using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;


namespace Uber.MmeMuxer
{
    public abstract class EncodeJob
    {
        private const int ProgressRefreshIntervalMs = 50;

        private class AsyncCallbackData
        {
            public byte[] Buffer = new byte[4096]; // 4 KB.
            public Process Process;
        }

        private double _previousProgress;

        protected readonly Mutex _mEncoderErrorOutputMutex = new Mutex();
        protected string _mEncoderErrorOutput;

        protected int TotalWorkLoad = 0;
        protected int ProcessedWorkLoad = 0; // Not counting the current sub-job's progress.
        protected int CurrentSubJobWorkLoad = 0;
        protected int ProcessedVideoIndex = 0;

        public delegate void ProgressDelegate(double progress);
        public delegate void IndexChangeDelegate(int index);
        public delegate void FrameRateDelegate(double frameRate);
        public delegate void FrameIndexDelegate(int frameIndex);

        public abstract void SaveJobToBatchFile(StreamWriter file);
        public abstract bool ProcessJob();

        public bool IsValid
        {
            get;
            protected set;
        }

        public bool HasAudio
        {
            get;
            protected set;
        }

        public int FrameCount
        {
            get;
            protected set;
        }

        protected void ProcessStdOutputReadingThread(object arg)
        {
            var process = arg as Process;
            if(process == null)
            {
                UmmApp.Instance.LogWarning("Invalid argument to the process standard output reader thread");
                return;
            }

            try
            {
                ProcessStdOutputReadingThreadImpl(process);
            }
            catch(Exception exception)
            {
                EntryPoint.RaiseException(exception);
            }
        }

        protected void ProcessStdOutputReadingThreadImpl(Process process)
        {
            _previousProgress = 0.0;

            var data = new AsyncCallbackData();
            data.Process = process;
            process.StandardOutput.BaseStream.BeginRead(data.Buffer, 0, data.Buffer.Length, AsyncStdOutputReadCallback, data);
        }

        private void SetSubJobProgress(double subJobProgress)
        {
            var subJobProcessed = (subJobProgress / 100.0) * CurrentSubJobWorkLoad;
            var totalProcessed = ProcessedWorkLoad + subJobProcessed;
            var jobProgress = 100.0 * (totalProcessed / (double)TotalWorkLoad);

            UmmApp.Instance.SetCurrentJobProgress(jobProgress);
        }

        protected virtual void OnVideoIndexChange(int videoIndex)
        {
        }

        private void AsyncStdOutputReadCallback(IAsyncResult result)
        {
            var data = result.AsyncState as AsyncCallbackData;
            if(data == null || data.Process == null)
            {
                return;
            }

            var process = data.Process;
            var stream = process.StandardOutput.BaseStream;
            var byteCount = stream.EndRead(result);
            var text = System.Text.Encoding.ASCII.GetString(data.Buffer, 0, byteCount);

            var progressMatch = UmmApp.MEncoderProgressRegEx.Match(text);
            if(progressMatch.Success)
            {
                UmmApp.Instance.SetJobAsStarted();

                var progressString = progressMatch.Groups[1].Captures[0].Value;
                var progress = 0;
                if(int.TryParse(progressString, out progress))
                {
                    if(progress < _previousProgress)
                    {
                        ++ProcessedVideoIndex;
                        UmmApp.Instance.SetCurrentSubJobFrameIndex(ProcessedVideoIndex);
                    }
                    _previousProgress = progress;

                    SetSubJobProgress((double)progress);
                }
            }

            var frameRateMatch = UmmApp.MEncoderFrameRateRegEx.Match(text);
            if(frameRateMatch.Success)
            {
                var frameRateString = frameRateMatch.Groups[1].Captures[0].Value + "." + frameRateMatch.Groups[2].Captures[0].Value;
                var frameRate = 0.0;
                if(double.TryParse(frameRateString, out frameRate))
                {
                    UmmApp.Instance.SetCurrentSubJobFrameRate(frameRate);
                }
            }

            var frameIndexMatch = UmmApp.MEncoderFrameIndexRegEx.Match(text);
            if(frameIndexMatch.Success)
            {
                var frameIndexString = frameIndexMatch.Groups[1].Captures[0].Value;
                var frameIndex = 0;
                if(int.TryParse(frameIndexString, out frameIndex))
                {
                    UmmApp.Instance.SetCurrentSubJobFrameIndex(frameIndex);
                }
            }

            Thread.Sleep(ProgressRefreshIntervalMs);

            if(!process.HasExited)
            {
                stream.BeginRead(data.Buffer, 0, data.Buffer.Length, AsyncStdOutputReadCallback, data);
            }
        }
        
        protected void ProcessErrOutputReadingThread(object arg)
        {
            var process = arg as Process;
            if(process == null)
            {
                UmmApp.Instance.LogWarning("Invalid argument to the process error output reader thread");
                return;
            }

            try
            {
                ProcessErrOutputReadingThreadImpl(process);
            }
            catch(Exception exception)
            {
                EntryPoint.RaiseException(exception);
            }
        }

        protected void ProcessErrOutputReadingThreadImpl(Process process)
        {
            var data = new AsyncCallbackData();
            data.Process = process;
            process.StandardError.BaseStream.BeginRead(data.Buffer, 0, data.Buffer.Length, AsyncErrOutputReadCallback, data);
        }

        private void AsyncErrOutputReadCallback(IAsyncResult result)
        {
            var data = result.AsyncState as AsyncCallbackData;
            if(data == null || data.Process == null)
            {
                return;
            }

            var process = data.Process;
            var stream = process.StandardError.BaseStream;
            var byteCount = stream.EndRead(result);
            var text = System.Text.Encoding.ASCII.GetString(data.Buffer, 0, byteCount);

            if(UmmApp.Instance.GetConfig().DisplayMEncoderStdErr)
            {
                _mEncoderErrorOutputMutex.WaitOne();
                _mEncoderErrorOutput += text;
                _mEncoderErrorOutputMutex.ReleaseMutex();
            }

            if(!process.HasExited)
            {
                stream.BeginRead(data.Buffer, 0, data.Buffer.Length, AsyncErrOutputReadCallback, data);
            }
        }

        protected void ReadProcessOutputUntilDone(Process process)
        {
            //
            // Start reader threads.
            //
            var stdOutputReaderThread = new Thread(ProcessStdOutputReadingThread);
            stdOutputReaderThread.Start(process);

            var stdErrorReaderThread = new Thread(ProcessErrOutputReadingThread);
            stdErrorReaderThread.Start(process);

            Thread.Sleep(1000);

            // Wait for the process to finish.
            process.WaitForExit();

            //
            // Wait for reader threads to finish.
            //
            if(stdOutputReaderThread != null)
            {
                stdOutputReaderThread.Join();
            }

            if(stdErrorReaderThread != null)
            {
                stdErrorReaderThread.Join();
            }
        }

        protected void InitializeMEncoderErrorOutput()
        {
            _mEncoderErrorOutputMutex.WaitOne();
            _mEncoderErrorOutput = "";
            _mEncoderErrorOutputMutex.ReleaseMutex();
        }

        protected void DisplayMEncoderErrorOutput()
        {
            var errorOutput = "";
            _mEncoderErrorOutputMutex.WaitOne();
            errorOutput = _mEncoderErrorOutput;
            _mEncoderErrorOutputMutex.ReleaseMutex();
            if(errorOutput.Length == 0)
            {
                return;
            }

            var lines = errorOutput.Split(new[] { '\r', '\n' });
            var app = UmmApp.Instance;
            foreach(var line in lines)
            {
                if(!string.IsNullOrWhiteSpace(line))
                {
                    app.LogWarning("> " + line);
                }
            }
        }
    }

    public class AviSequenceEncodeJob : EncodeJob
    {
        private string _folderPath;
        private string _videoFilePath;
        private readonly List<string> _videoFilePaths = new List<string>();
        private readonly List<int> _videoFileFrameCounts = new List<int>();
        private string _audioFilePath;
        private bool _folderMode;
        private bool _aviHasAudio;

        private bool DoesNeedAdditionalPass
        {
            get { return _audioFilePath != null && _videoFilePaths.Count > 1; }
        }

        private const string TempFileName = "temp.avi";
        
        public AviSequenceEncodeJob(string folderPath, string videoFilePath, string audioFilePath)
        {
            _folderMode = folderPath != null;
            _folderPath = folderPath;
            _videoFilePath = videoFilePath;
            _audioFilePath = audioFilePath;
            _aviHasAudio = false;
            IsValid = false;
            HasAudio = audioFilePath != null;
            FrameCount = 0;
        }

        public static AviSequenceEncodeJob FromFile(string videoFilePath)
        {
            return new AviSequenceEncodeJob(null, videoFilePath, null);
        }

        public static AviSequenceEncodeJob FromFolder(string folderPath)
        {
            return new AviSequenceEncodeJob(folderPath, null, null);
        }

        public void Analyze()
        {
            if(!_folderMode)
            {
                AnalyzeFile();
            }
            else
            {
                AnalyzeFolder();
            }
        }

        public override void SaveJobToBatchFile(StreamWriter file)
        {
            var workDir = GetWorkDir();
            var outputFilePath = CreateOutputFilePath();
            var videoFilePaths = GetVideoFilePaths();
            var args = new MEncoderArguments();
            args.AviHasAudio = _aviHasAudio;
            args.ImageSequence = false;
            args.InputAudioPath = _audioFilePath != null ? Path.GetFullPath(_audioFilePath) : null;
            args.InputVideoPaths.AddRange(videoFilePaths);
            args.OutputFilePath = outputFilePath;
            args.UseSeparateAudioFile = (_audioFilePath != null) && (_videoFilePaths.Count == 1);
            UmmApp.Instance.WriteTobatchFile(file, workDir, args);

            if(DoesNeedAdditionalPass)
            {
                workDir = GetWorkDir();
                outputFilePath = CreateOutputFilePath(true);
                args = new MEncoderArguments();
                args.AviHasAudio = false;
                args.ImageSequence = false;
                args.InputAudioPath = Path.GetFullPath(_audioFilePath);
                args.InputVideoPaths.Add(TempFileName);
                args.OutputFilePath = outputFilePath;
                args.UseSeparateAudioFile = true;
                args.CodecOverride = true;
                args.Codec = VideoCodec.Copy;

                UmmApp.Instance.WriteTobatchFile(file, workDir, args);

                var tempFilePath = Path.Combine(workDir, TempFileName);
                file.Write("del ");
                file.WriteLine(tempFilePath);
            }
        }

        public override bool ProcessJob()
        {
            // This job only has 1 sub-job.
            TotalWorkLoad = DoesNeedAdditionalPass ? (FrameCount * 2) : FrameCount;
            ProcessedWorkLoad = 0;
            CurrentSubJobWorkLoad = _videoFileFrameCounts[0];

            InitializeMEncoderErrorOutput();

            // @NOTE: MEncoder can't mux multiple .avi files with a single .wav file.
            var workDir = GetWorkDir();
            var outputFilePath = CreateOutputFilePath();
            var videoFilePaths = GetVideoFilePaths();
            var args = new MEncoderArguments();
            args.AviHasAudio = _aviHasAudio;
            args.ImageSequence = false;
            args.InputAudioPath = _audioFilePath != null ? Path.GetFullPath(_audioFilePath) : null;
            args.InputVideoPaths.AddRange(videoFilePaths);
            args.OutputFilePath = outputFilePath;
            args.UseSeparateAudioFile = (_audioFilePath != null) && (_videoFilePaths.Count == 1);

            var info = UmmApp.Instance.CreateMEncoderProcessStartInfo(workDir, args);
            var process = Process.Start(info);
            if(process == null)
            {
                return false;
            }

            ReadProcessOutputUntilDone(process);

            // Update progress.
            ProcessedWorkLoad += CurrentSubJobWorkLoad;

            // Display error output, if any.
            DisplayMEncoderErrorOutput();

            if(DoesNeedAdditionalPass)
            {
                ProcessFinalAudioMuxJob();
            }

            return true;
        }

        private bool ProcessFinalAudioMuxJob()
        {
            InitializeMEncoderErrorOutput();

            var workDir = GetWorkDir();
            var outputFilePath = CreateOutputFilePath(true);
            var args = new MEncoderArguments();
            args.AviHasAudio = false;
            args.ImageSequence = false;
            args.InputAudioPath = Path.GetFullPath(_audioFilePath);
            args.InputVideoPaths.Add(TempFileName);
            args.OutputFilePath = outputFilePath;
            args.UseSeparateAudioFile = true;
            args.CodecOverride = true;
            args.Codec = VideoCodec.Copy;

            var info = UmmApp.Instance.CreateMEncoderProcessStartInfo(workDir, args);
            var process = Process.Start(info);
            if(process == null)
            {
                return false;
            }

            ReadProcessOutputUntilDone(process);
            DisplayMEncoderErrorOutput();

            var tempFilePath = Path.Combine(workDir, TempFileName);
            if(File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            return true;
        }


        protected override void OnVideoIndexChange(int videoIndex)
        {
            if(videoIndex >= _videoFileFrameCounts.Count)
            {
                return;
            }

            ProcessedWorkLoad += CurrentSubJobWorkLoad;
            CurrentSubJobWorkLoad = _videoFileFrameCounts[videoIndex];
        }

        private string GetWorkDir()
        {
            if(!_folderMode)
            {
                return Path.GetDirectoryName(_videoFilePath);
            }

            return Path.GetFullPath(_folderPath);
        }

        private List<string> GetVideoFilePaths()
        {
            if(!_folderMode)
            {
                return new List<string> { _videoFilePath };
            }

            var fileNames = new List<string>();
            foreach(var videoFilePath in _videoFilePaths)
            {
                fileNames.Add(Path.GetFileName(videoFilePath));
            }

            return fileNames;
        }

        private string CreateOutputFilePath(bool finalAudioMux = false)
        {
            if(DoesNeedAdditionalPass && !finalAudioMux)
            {
                return TempFileName;
            }

            var outputFilePath = "";
            var config = UmmApp.Instance.GetConfig();
            if(_folderMode)
            {
                var inputParentFolderPath = Path.GetDirectoryName(_folderPath);
                var inputFolderName = Path.GetFileName(_folderPath);
                outputFilePath = Path.Combine(config.OutputAllFilesToSameFolder ? config.OutputFolderPath : inputParentFolderPath, inputFolderName + ".avi");

                return outputFilePath;
            }
            
            var inputFileName = Path.GetFileName(_videoFilePath);
            var inputFolderPath = Path.GetDirectoryName(_videoFilePath);
            var outputFileName = UmmApp.Instance.CreateOutputFileName(inputFileName);
            outputFilePath = Path.Combine(config.OutputAllFilesToSameFolder ? config.OutputFolderPath : inputFolderPath, outputFileName);
            outputFilePath = UmmApp.Instance.ValidateAndFixOutputFilePath(outputFilePath);

            return outputFilePath;
        }

        private void AnalyzeFile()
        {
            var info = new AviFileInfo(_videoFilePath);
            if(!info.IsValid)
            {
                return;
            }

            _aviHasAudio = info.AudioStreams >= 1;
            IsValid = true;
            HasAudio = (_audioFilePath != null) ? true : (info.AudioStreams >= 1);
            FrameCount = info.FrameCount;
            _videoFilePaths.Add(_videoFilePath);
            _videoFileFrameCounts.Add(info.FrameCount);
        }

        private void AnalyzeFolder()
        {
            var videoFilePaths = Directory.GetFiles(_folderPath, "*.avi", SearchOption.TopDirectoryOnly);
            if(videoFilePaths.Length == 0)
            {
                return;
            }

            foreach(var videoFilePath in videoFilePaths)
            {
                var info = new AviFileInfo(videoFilePath);
                if(!info.IsValid)
                {
                    continue;
                }

                _videoFilePaths.Add(videoFilePath);
                _videoFileFrameCounts.Add(info.FrameCount);
                _aviHasAudio = _aviHasAudio || (info.AudioStreams > 0);
                FrameCount += info.FrameCount;
            }

            // Keep the first audio file we find.
            foreach(var audioExtension in UmmApp.AudioExtensions)
            {
                var audioFilePaths = Directory.GetFiles(_folderPath, "*" + audioExtension, SearchOption.TopDirectoryOnly);
                if(audioFilePaths.Length > 0)
                {
                    _audioFilePath = audioFilePaths[0];
                    break;
                }
            }

            IsValid = FrameCount > 0;
            HasAudio = (_audioFilePath != null) || _aviHasAudio;
        }
    }

    public class ImageSequenceEncodeJob : EncodeJob
    {
        private enum ImageType
        {
            Normal,
            Depth,
            Stencil
        }

        private class ImageSequence
        {
            public readonly List<string> ImageFilePaths = new List<string>();
            public string ImageSequenceRegEx;
            public string ImageSequencePath;
            public string FirstImageName;
            public bool Monochrome;
            public ImageType Type = ImageType.Normal;
        }

        private string _folderPath;
        private string _audioFilePath;
        private readonly List<ImageSequence> _imageSequences = new List<ImageSequence>();

        public int SequenceCount
        {
            get { return _imageSequences.Count; }
        }

        public ImageSequenceEncodeJob(string folderPath)
        {
            _folderPath = folderPath;
            IsValid = false;
            HasAudio = false;
            FrameCount = 0;
        }

        public void AnalyzeFolder()
        {
            var imageExtensions = UmmApp.ImageExtensions;
            var audioExtensions = UmmApp.AudioExtensions;

            foreach(var imageExtension in imageExtensions)
            {
                var imageFilePaths = Directory.GetFiles(_folderPath, "*" + imageExtension, SearchOption.TopDirectoryOnly);
                if(imageFilePaths.Length == 0)
                {
                    continue;
                }

                FrameCount += imageFilePaths.Length;

                var sequences = SplitSequences(imageFilePaths);
                _imageSequences.AddRange(sequences);
            }

            // Keep the first audio file we find.
            foreach(var audioExtension in audioExtensions)
            {
                var audioFilePaths = Directory.GetFiles(_folderPath, "*" + audioExtension, SearchOption.TopDirectoryOnly);
                if(audioFilePaths.Length > 0)
                {
                    _audioFilePath = audioFilePaths[0];
                    break;
                }
            }

            IsValid = FrameCount > 0;
            HasAudio = _audioFilePath != null;
        }

        public override void SaveJobToBatchFile(StreamWriter file)
        {
            foreach(var sequence in _imageSequences)
            {
                var config = UmmApp.Instance.GetConfig();
                
                var args = new MEncoderArguments();
                args.AviHasAudio = false;
                args.ImageSequence = true;
                args.InputAudioPath = HasAudio ? Path.GetFullPath(_audioFilePath) : null;
                args.InputImagesPath = sequence.ImageSequencePath;
                args.OutputFilePath = CreateOutputFilePath(sequence, config);
                args.UseSeparateAudioFile = HasAudio && !sequence.Monochrome;
                args.Monochrome = sequence.Monochrome;

                var folderPath = Path.GetFullPath(_folderPath);
                UmmApp.Instance.WriteTobatchFile(file, folderPath, args);
            } 
        }

        public override bool ProcessJob()
        {
            TotalWorkLoad = 0;
            ProcessedWorkLoad = 0;
            foreach(var sequence in _imageSequences)
            {
                TotalWorkLoad += sequence.ImageFilePaths.Count;
            }

            foreach(var sequence in _imageSequences)
            {
                CurrentSubJobWorkLoad = sequence.ImageFilePaths.Count;

                InitializeMEncoderErrorOutput();

                var config = UmmApp.Instance.GetConfig();
                var outputFilePath = CreateOutputFilePath(sequence, config);
                var args = new MEncoderArguments();
                args.AviHasAudio = false;
                args.ImageSequence = true;
                args.InputAudioPath = HasAudio ? Path.GetFullPath(_audioFilePath) : null;
                args.InputImagesPath = sequence.ImageSequencePath;
                args.OutputFilePath = outputFilePath;
                args.UseSeparateAudioFile = HasAudio && !sequence.Monochrome;
                args.Monochrome = sequence.Monochrome;

                var folderPath = Path.GetFullPath(_folderPath);
                var info = UmmApp.Instance.CreateMEncoderProcessStartInfo(folderPath, args);
                var process = Process.Start(info);
                if(process == null)
                {
                    return false;
                }

                ReadProcessOutputUntilDone(process);

                // Update progress.
                ProcessedWorkLoad += CurrentSubJobWorkLoad;

                // Display error output, if any.
                DisplayMEncoderErrorOutput();
            } 

            return true;
        }

        private string CreateSequenceRegExString(string fileName)
        {
            var regExString = "";
            foreach(var c in fileName)
            {
                if(char.IsDigit(c))
                {
                    regExString += "\\d";
                }
                // RegEx special characters.
                else if(c == '.' || c == '(' || c == ')')
                {
                    regExString += "\\";
                    regExString += c;
                }
                else
                {
                    regExString += c;
                }
            }

            return regExString;
        }

        private string CreateMEncoderSequenceString(string fileName)
        {
            return UmmApp.MEncoderSequenceMatchRegEx.Replace(fileName, UmmApp.MEncoderSequenceReplacement);
        }

        private string CreateOutputFileName(string fileName, ImageType imageType)
        {
            if(UmmApp.Instance.GetConfig().FileNamingUseImageName)
            {
                return CreateOutputFileNameFromFile(fileName, imageType);
            }

            return CreateOutputFileNameFromDirectory(fileName, imageType);
        }

        private string CreateOutputFilePath(ImageSequence sequence, UmmConfig config)
        {
            var outputFileName = UmmApp.Instance.CreateOutputFileName(CreateOutputFileName(sequence.FirstImageName, sequence.Type));
            var outputFilePath = Path.Combine(config.OutputAllFilesToSameFolder ? config.OutputFolderPath : Path.GetDirectoryName(_folderPath), outputFileName);
            outputFilePath = UmmApp.Instance.ValidateAndFixOutputFilePath(outputFilePath);

            return outputFilePath;
        }

        private string CreateOutputFileNameFromFile(string fileName, ImageType imageType)
        {
            return UmmApp.MMESequenceMatchRegEx.Replace(fileName, UmmApp.MMESequenceReplacement) + ".avi";
        }

        private string CreateOutputFileNameFromDirectory(string fileName, ImageType imageType)
        {
            var folderName = Path.GetFileName(_folderPath);
            
            if(imageType == ImageType.Depth)
            {
                return folderName + ".depth.avi";
            }

            if(imageType == ImageType.Stencil)
            {
                return folderName + ".stencil.avi";
            }

            return folderName + ".avi";
        }

        private List<ImageSequence> SplitSequences(string[] filePaths)
        {
            var sequences = new List<ImageSequence>();

            for(var i = 0; i < filePaths.Length;)
            {
                var fileNameI = Path.GetFileName(filePaths[i]).ToLower();
                var regExString = CreateSequenceRegExString(fileNameI);
                var sequenceRegEx = new Regex(regExString, RegexOptions.Compiled);
                var imagePaths = new List<string>();
                imagePaths.Add(filePaths[i]);

                for(var j = i + 1; j < filePaths.Length; ++j)
                {
                    var fileNameJ = Path.GetFileName(filePaths[j]).ToLower();
                    if(sequenceRegEx.IsMatch(fileNameJ))
                    {
                        imagePaths.Add(filePaths[j]);
                        i = j;
                    }
                    else
                    {
                        i = j;
                        break;
                    }
                }

                if(imagePaths.Count == 1)
                {
                    continue;
                }

                var firstImageName = fileNameI;
                var sequence = new ImageSequence();
                var hasDepth = firstImageName.Contains(".depth.");
                var hasStencil = firstImageName.Contains(".stencil.");
                var sequenceType = hasDepth ? ImageType.Depth : (hasStencil ? ImageType.Stencil : ImageType.Normal);
                sequence.FirstImageName = firstImageName;
                sequence.ImageFilePaths.AddRange(imagePaths);
                sequence.ImageSequencePath = CreateMEncoderSequenceString(firstImageName);
                sequence.ImageSequenceRegEx = regExString;
                sequence.Monochrome = hasDepth || hasStencil;
                sequence.Type = sequenceType;
                sequences.Add(sequence);

                if(i == filePaths.Length - 1)
                {
                    break;
                }
            }

            return sequences;
        }
    }

    public class ReflexEncodeJob : EncodeJob
    {
        private enum ImageType
        {
            Colour,
            Depth
        }

        private class ImageSequence
        {
            public readonly List<string> ImageFilePaths = new List<string>();
            public string DemoCutFolderName;
            public string ImageSequencePath; // Input file name for MEncoder.
            public string FolderPath; // The folder path containing the images.
            public ImageType Type = ImageType.Colour;
            public int FrameRate = 0;
        }

        private string _folderPath;
        private readonly List<ImageSequence> _imageSequences = new List<ImageSequence>();

        public int SequenceCount
        {
            get { return _imageSequences.Count; }
        }

        public ReflexEncodeJob(string folderPath)
        {
            _folderPath = folderPath;
            IsValid = false;
            HasAudio = false;
            FrameCount = 0;
        }

        public void Analyze()
        {
            var cutFolders = Directory.GetDirectories(_folderPath, "time*_framerate*", SearchOption.TopDirectoryOnly);
            foreach(var cutFolder in cutFolders)
            {
                var cutFolderName = Path.GetFileName(cutFolder);
                var frameRateIndex = cutFolderName.IndexOf("framerate") + "framerate".Length;
                var frameRate = int.Parse(cutFolderName.Substring(frameRateIndex));

                var colourFolder = Path.Combine(cutFolder, "colour");
                if(Directory.Exists(colourFolder))
                {
                    AddImageSequence(colourFolder, "colour", ImageType.Colour, cutFolderName, frameRate);
                }

                var depthFolder = Path.Combine(cutFolder, "depth");
                if(Directory.Exists(depthFolder))
                {
                    AddImageSequence(depthFolder, "depth", ImageType.Depth, cutFolderName, frameRate);
                }
            }

            IsValid = FrameCount > 0;
        }

        public override void SaveJobToBatchFile(StreamWriter file)
        {
            var i = 0;
            foreach(var sequence in _imageSequences)
            {
                var config = UmmApp.Instance.GetConfig();

                var args = new MEncoderArguments();
                args.AviHasAudio = false;
                args.ImageSequence = true;
                args.InputAudioPath = null;
                args.InputImagesPath = sequence.ImageSequencePath;
                args.OutputFilePath = CreateOutputFilePath(sequence, config, i);
                args.UseSeparateAudioFile = false;
                args.Monochrome = sequence.Type == ImageType.Depth;
                args.InputFrameRate = sequence.FrameRate;
                args.OutputFrameRate = sequence.FrameRate;

                UmmApp.Instance.WriteTobatchFile(file, sequence.FolderPath, args);

                ++i;
            } 
        }

        public override bool ProcessJob()
        {
            TotalWorkLoad = 0;
            ProcessedWorkLoad = 0;
            foreach(var sequence in _imageSequences)
            {
                TotalWorkLoad += sequence.ImageFilePaths.Count;
            }

            var i = 0;
            foreach(var sequence in _imageSequences)
            {
                CurrentSubJobWorkLoad = sequence.ImageFilePaths.Count;

                InitializeMEncoderErrorOutput();

                var config = UmmApp.Instance.GetConfig();
                var outputFilePath = CreateOutputFilePath(sequence, config, i);
                var args = new MEncoderArguments();
                args.AviHasAudio = false;
                args.ImageSequence = true;
                args.InputAudioPath = null;
                args.InputImagesPath = sequence.ImageSequencePath;
                args.OutputFilePath = outputFilePath;
                args.UseSeparateAudioFile = false;
                args.Monochrome = sequence.Type == ImageType.Depth;
                args.InputFrameRate = sequence.FrameRate;
                args.OutputFrameRate = sequence.FrameRate;

                var info = UmmApp.Instance.CreateMEncoderProcessStartInfo(sequence.FolderPath, args);
                var process = Process.Start(info);
                if(process == null)
                {
                    return false;
                }

                ReadProcessOutputUntilDone(process);

                // Update progress.
                ProcessedWorkLoad += CurrentSubJobWorkLoad;

                // Display error output, if any.
                DisplayMEncoderErrorOutput();

                ++i;
            }

            return true;
        }

        private string CreateOutputFileName(ImageSequence sequence, int sequenceIndex)
        {
            var demoName = Path.GetFileName(_folderPath);
            var cutName = "timeunknown";
            var type = sequence.Type == ImageType.Colour ? "colour" : "depth";
            var underscoreIdx = sequence.DemoCutFolderName.IndexOf('_');
            if(underscoreIdx >= 0 && underscoreIdx < sequence.DemoCutFolderName.Length - 1)
            {
                cutName = sequence.DemoCutFolderName.Substring(0, underscoreIdx);
            }

            return demoName + "_" + cutName + "_" + type + ".avi";
        }

        private string CreateOutputFilePath(ImageSequence sequence, UmmConfig config, int sequenceIndex)
        {
            var outputFileName = UmmApp.Instance.CreateOutputFileName(CreateOutputFileName(sequence, sequenceIndex));
            var outputFilePath = Path.Combine(config.OutputAllFilesToSameFolder ? config.OutputFolderPath : Path.GetDirectoryName(_folderPath), outputFileName);
            outputFilePath = UmmApp.Instance.ValidateAndFixOutputFilePath(outputFilePath);

            return outputFilePath;
        }

        private void AddImageSequence(string folderPath, string name, ImageType imageType, string cutFolderName, int frameRate)
        {
            // Find images in one of the supported formats.
            string[] imageFilePaths = null;
            var extension = "";
            var formatIndex = -1;
            for(var i = 0; i < FileFormats.Length; ++i)
            {
                var format = FileFormats[i];
                imageFilePaths = Directory.GetFiles(folderPath, "*" + format.Extension, SearchOption.TopDirectoryOnly);
                if(imageFilePaths.Length > 0)
                {
                    extension = format.Extension;
                    formatIndex = i;
                    break;
                }
            }

            if(imageFilePaths == null)
            {
                return;
            }

            // Create a list with the real index numbers since alphanumeric sorting
            // of indices without leading zeroes would be wrong.
            var fileNameRegEx = new Regex(name + "_" + @"(\d+)", RegexOptions.Compiled);
            var imageList = new List<FileNameSortInfo>();
            var highestNumber = 0;
            foreach(var imageFilePath in imageFilePaths)
            {
                var fileName = Path.GetFileName(imageFilePath);
                var match = fileNameRegEx.Match(fileName);
                if(!match.Success)
                {
                    continue;
                }

                var numberString = match.Groups[1].Value;
                var number = int.Parse(numberString);
                highestNumber = Math.Max(highestNumber, number);
                imageList.Add(new FileNameSortInfo(imageFilePath, number, numberString.Length));
            }
            if(imageList.Count == 0)
            {
                return;
            }

            // Sort the list by increasing image index.
            imageList.Sort((a, b) => a.Number.CompareTo(b.Number));

            // Fix image names so that MEncoder gets things right...
            var digitCount = GetDigitCount(highestNumber);
            foreach(var image in imageList)
            {
                if(image.NumberLength == digitCount)
                {
                    continue;
                }

                var imageDigitCount = GetDigitCount(image.Number);
                var leadingZeroes = new string('0', digitCount - imageDigitCount);
                var newFileName = name + "_" + leadingZeroes + image.Number.ToString() + extension;
                var newFilePath = Path.Combine(folderPath, newFileName);
                File.Move(image.FilePath, newFilePath);
                image.FilePath = newFilePath;
            }

            // Watch out for Reflex outputting images with the wrong file extension.
            var finalExtension = extension;
            if(!FileFormats[formatIndex].IsValid(imageList[0].FilePath))
            {
                // Let's find the real file format.
                bool correctFormatFound = false;
                for(var i = 0; i < FileFormats.Length; ++i)
                {
                    if(i != formatIndex && FileFormats[i].IsValid(imageList[0].FilePath))
                    {
                        correctFormatFound = true;
                        finalExtension = FileFormats[i].Extension;
                        break;
                    }
                }
                if(!correctFormatFound)
                {
                    return;
                }

                // Rename the files and update our image sequence structure.
                foreach(var image in imageList)
                {
                    var newFilePath = Path.ChangeExtension(image.FilePath, finalExtension);
                    File.Move(image.FilePath, newFilePath);
                    image.FilePath = newFilePath;
                }
                UmmApp.Instance.LogWarning("Detected {0} files disguised as {1} files, fixed that for you.", finalExtension, extension);
            }

            var sequence = new ImageSequence();
            foreach(var image in imageList)
            {
                sequence.ImageFilePaths.Add(image.FilePath);
            }
            sequence.Type = imageType;
            sequence.ImageSequencePath = name + "_*" + finalExtension;
            sequence.DemoCutFolderName = cutFolderName;
            sequence.FolderPath = folderPath;
            sequence.FrameRate = frameRate;
            _imageSequences.Add(sequence);

            FrameCount += sequence.ImageFilePaths.Count;
        }

        private static int ReadFileHeader(string filePath)
        {
            using(var reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
            {                
                return reader.ReadInt32();
            }
        }

        private static bool IsFilePNG(string filePath)
        {
            return ReadFileHeader(filePath) == 1196314761; // "�PNG"
        }

        private static bool IsFileTGA(string filePath)
        {
            return ReadFileHeader(filePath) == 131072; // 00 00 02 00
        }

        private static bool IsFileJPG(string filePath)
        {
            var file = File.Open(filePath, FileMode.Open);
            using(var reader = new BinaryReader(file))
            {
                var number1 = reader.ReadInt32();
                file.Position = 6;
                var number2 = reader.ReadInt32();

                return number1 == -520103681 && // FF D8 FF E0
                    number2 == 1179207242;      // "JFIF"
            }
        }

        private class FileFormat
        {
            public delegate bool IsValidDelegate(string filePath);

            public FileFormat(string extension, IsValidDelegate isValid)
            {
                Extension = extension;
                IsValid = isValid;
            }

            public string Extension;
            public IsValidDelegate IsValid;
        }

        private static FileFormat[] FileFormats = new FileFormat[]
        {
            new FileFormat(".tga", IsFileTGA),
            new FileFormat(".png", IsFilePNG),
            new FileFormat(".jpg", IsFileJPG),
            new FileFormat(".jpeg", IsFileJPG)
        };
        
        private int GetDigitCount(int number)
        {
            var count = 0; 
            do 
            { 
                ++count;
            }
            while((number /= 10) >= 1);

            return count;
        }

        private class FileNameSortInfo
        {
            public FileNameSortInfo(string filePath, int number, int numberLength)
            {
                FilePath = filePath;
                Number = number;
                NumberLength = numberLength;
            }

            public string FilePath;
            public int Number;
            public int NumberLength;
        }
    }
}