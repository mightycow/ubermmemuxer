using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;


namespace Uber.MmeMuxer
{
    public static class UmmVersion
    {
        public static readonly string String = "0.1.2";
    }

    public class MEncoderArguments
    {
        public bool ImageSequence;
        public bool AviHasAudio;
        public bool UseSeparateAudioFile;
        public bool Monochrome;
        public readonly List<string> InputVideoPaths = new List<string>();
        public string InputImagesPath;
        public string InputAudioPath;
        public string OutputFilePath;
        public bool CodecOverride = false;
        public VideoCodec Codec;
    }

    public class UmmConfig
    {
        public VideoCodec ColorCodec = VideoCodec.Lagarith;
        public string CustomColorOptionsLavc = "vcodec=ffv1";
        public string CustomColorVfwCodecName = "";
        public VideoCodec MonochromeCodec = VideoCodec.Raw;
        public string CustomMonochromeOptionsLavc = "";
        public string CustomMonochromeVfwCodecName = "";
        public string MEncoderFilePath = @"C:\Program Files (x86)\MPlayer\mencoder.exe";
        public int FrameRate = 60;
        public int OutputFrameRate = 60;
        public bool ShowColorCodecDialog = false;
        public bool ShowMonochromeCodecDialog = false;
        public bool DisplayMEncoderStdErr = true;
        public bool OutputAllFilesToSameFolder = false;
        public string OutputFolderPath = "";
        public FileNamingMethod FileNamingPolicy = FileNamingMethod.NoChange;
        public string FileNamingPrefix = "";
        public string FileNamingSuffix = "";
        public string FileNamingRegExpMatch = "(.+)\\.(.+)";
        public string FileNamingRegExpReplacement = "$1_lag.avi";
        public int FramesToSkip = 2;
        public bool FileNamingUseImageName = false;
    }

    public partial class UmmApp : AppBase<UmmConfig>
    {
        public static UmmApp Instance
        {
            get;
            private set;
        }

        public static readonly Regex MEncoderSequenceMatchRegEx = new Regex(@"\d+", RegexOptions.Compiled);
        public static readonly string MEncoderSequenceReplacement = "*";

        public static readonly Regex MMESequenceMatchRegEx = new Regex(@"(.+)\.(\d+)\.(tga|bmp|png|jpg|jpeg)", RegexOptions.Compiled);
        public static readonly string MMESequenceReplacement = "$1";

        // MEncoder progress format: "( 1%)" --> "(99%)".
        public static readonly Regex MEncoderProgressRegEx = new Regex(@"\( ?(\d+)%\)", RegexOptions.Compiled);

        // MEncoder speed format: "0.00fps" --> "49.26fps".
        public static readonly Regex MEncoderFrameRateRegEx = new Regex(@"(\d+)\.(\d+)fps", RegexOptions.Compiled);

        // MEncoder frame format: "1f" --> "160f".
        public static readonly Regex MEncoderFrameIndexRegEx = new Regex(@"(\d+)f", RegexOptions.Compiled);

        public static readonly List<string> ImageExtensions = new List<string>
        {
            ".tga",
            ".png",
            ".jpg",
            ".jpeg"
        };

        public static readonly List<string> AudioExtensions = new List<string>
        {
            ".wav"
        };

        public bool ColorCodecDialogShown = false;
        public bool MonochromeCodecDialogShown = false;

        private ListView _jobsListView;
        private Brush _jobsListViewBackground;
        private List<EncodeJob> _jobs = new List<EncodeJob>();

        private static RoutedCommand _deleteFolderCommand = new RoutedCommand();

        private enum VideoStreamType
        {
            Invalid,
            Avi,
            TargaSequence,
            PingSequence,
            JpegSequence
        }

        private class JobDisplayInfo
        {
            public JobDisplayInfo()
            {
                Status = "waiting";
                Name = "N/A";
                FrameCount = 0;
                HasAudio = false;
                VideoCount = 0;
            }

            public EncodeJob Job { get; set; }
            public string Status { get; set; }
            public string Name { get; set; }
            public int FrameCount { get; set; }
            public bool HasAudio { get; set; }
            public int VideoCount { get; set; }
        }

        public UmmApp(string[] cmdLineArgs) : base(cmdLineArgs)
        {
            Instance = this;
            AppVersion = UmmVersion.String;
            AboutIcon = MmeMuxer.Properties.Resources.UMMIcon;

            var manageJobsTab = new TabItem();
            manageJobsTab.Header = "Manage Jobs";
            manageJobsTab.Content = CreateJobsTab();

            var settingsTab = new TabItem();
            settingsTab.Header = "Settings";
            settingsTab.Content = CreateSettingsTab();

            var progressTab = new TabItem();
            ProgressTab = progressTab;
            progressTab.Header = "Progress";
            progressTab.Content = CreateProgressTab();
            
            var tabControl = new TabControl();
            TabControl = tabControl;
            tabControl.HorizontalAlignment = HorizontalAlignment.Stretch;
            tabControl.VerticalAlignment = VerticalAlignment.Stretch;
            tabControl.Margin = new Thickness(5);
            tabControl.Items.Add(manageJobsTab);
            tabControl.Items.Add(settingsTab);

            var exitMenuItem = new MenuItem();
            exitMenuItem.Header = "E_xit";
            exitMenuItem.Click += (obj, arg) => OnQuit();
            exitMenuItem.ToolTip = new ToolTip { Content = "Close the program" };
            /*
            var openFolderMenuItem = new MenuItem();
            openFolderMenuItem.Header = "Open MME Mux Folder...";
            openFolderMenuItem.Click += (obj, arg) => OnOpenMuxFolder();
            openFolderMenuItem.ToolTip = new ToolTip { Content = "Open a demo folder for processing/analysis" };
            */
            var fileMenuItem = new MenuItem();
            fileMenuItem.Header = "_File";
            //fileMenuItem.Items.Add(openFolderMenuItem);
            //fileMenuItem.Items.Add(new Separator());
            fileMenuItem.Items.Add(exitMenuItem);

            var log = new Log(150);
            Log = log;

            var viewHelpMenuItem = new MenuItem();
            viewHelpMenuItem.Header = "View Help";
            viewHelpMenuItem.Click += (obj, arg) => ViewHelp();
            viewHelpMenuItem.ToolTip = new ToolTip { Content = "Opens README.txt with your current text editor" };

            var aboutMenuItem = new MenuItem();
            aboutMenuItem.Header = "_About Uber MME Muxer";
            aboutMenuItem.Click += (obj, arg) => ShowAboutWindow();
            aboutMenuItem.ToolTip = new ToolTip { Content = "Learn more about this awesome application" };

            var helpMenuItem = new MenuItem();
            helpMenuItem.Header = "_Help";
            helpMenuItem.Items.Add(viewHelpMenuItem);
            helpMenuItem.Items.Add(new Separator());
            helpMenuItem.Items.Add(aboutMenuItem);

            var mainMenu = new Menu();
            mainMenu.IsMainMenu = true;
            mainMenu.Items.Add(fileMenuItem);
            mainMenu.Items.Add(log.LogMenuItem);
            mainMenu.Items.Add(helpMenuItem);

            var logGroupBox = new GroupBox();
            logGroupBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            logGroupBox.VerticalAlignment = VerticalAlignment.Stretch;
            logGroupBox.Margin = new Thickness(5);
            logGroupBox.Header = "Log";
            logGroupBox.Content = log.LogListBox;

            var progressBar = new ProgressBar();
            ProgressBar = progressBar;
            progressBar.HorizontalAlignment = HorizontalAlignment.Stretch;
            progressBar.VerticalAlignment = VerticalAlignment.Bottom;
            progressBar.Margin = new Thickness(5);
            progressBar.Height = 20;
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;

            var cancelJobButton = new Button();
            CancelJobButton = cancelJobButton;
            cancelJobButton.HorizontalAlignment = HorizontalAlignment.Right;
            cancelJobButton.VerticalAlignment = VerticalAlignment.Center;
            cancelJobButton.Width = 70;
            cancelJobButton.Height = 25;
            cancelJobButton.Content = "Cancel";
            cancelJobButton.Click += (obj, args) => OnCancelJobClicked();

            var progressPanel = new DockPanel();
            progressPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            progressPanel.VerticalAlignment = VerticalAlignment.Bottom;
            progressPanel.LastChildFill = true;
            progressPanel.Children.Add(cancelJobButton);
            progressPanel.Children.Add(progressBar);
            DockPanel.SetDock(cancelJobButton, Dock.Right);

            var progressGroupBox = new GroupBox();
            ProgressGroupBox = progressGroupBox;
            progressGroupBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            progressGroupBox.VerticalAlignment = VerticalAlignment.Center;
            progressGroupBox.Margin = new Thickness(5, 0, 5, 0);
            progressGroupBox.Header = "Progress";
            progressGroupBox.Content = progressPanel;
            progressGroupBox.Visibility = Visibility.Collapsed;

            var jobsGridView = new GridView();
            jobsGridView.AllowsColumnReorder = false;
            jobsGridView.Columns.Add(new GridViewColumn { Header = "Status", Width = 60, DisplayMemberBinding = new Binding("Status") });
            jobsGridView.Columns.Add(new GridViewColumn { Header = "File or Folder Name", Width = 330, DisplayMemberBinding = new Binding("Name") });
            jobsGridView.Columns.Add(new GridViewColumn { Header = "Frames", Width = 60, DisplayMemberBinding = new Binding("FrameCount") });
            jobsGridView.Columns.Add(new GridViewColumn { Header = "Audio?", Width = 50, DisplayMemberBinding = new Binding("HasAudio") });
            jobsGridView.Columns.Add(new GridViewColumn { Header = "Videos", Width = 60, DisplayMemberBinding = new Binding("VideoCount") });

            var jobsListView = new ListView();
            _jobsListView = jobsListView;
            _jobsListViewBackground = jobsListView.Background;
            jobsListView.HorizontalAlignment = HorizontalAlignment.Stretch;
            jobsListView.VerticalAlignment = VerticalAlignment.Stretch;
            jobsListView.Margin = new Thickness(5);
            jobsListView.Width = 570;
            jobsListView.AllowDrop = true;
            jobsListView.View = jobsGridView;
            jobsListView.SelectionMode = SelectionMode.Extended;
            jobsListView.DragEnter += OnMuxFolderListBoxDragEnter;
            jobsListView.Drop += OnMuxFolderListBoxDragDrop;
            jobsListView.Initialized += (obj, arg) => { _jobsListViewBackground = _jobsListView.Background; };
            jobsListView.Foreground = new SolidColorBrush(Colors.Black);
            InitFolderListDeleteCommand();

            var jobsListGroupBox = new GroupBox();
            jobsListGroupBox.Header = "Jobs List";
            jobsListGroupBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            jobsListGroupBox.VerticalAlignment = VerticalAlignment.Stretch;
            jobsListGroupBox.Margin = new Thickness(5);
            jobsListGroupBox.Content = jobsListView;

            var centerPartPanel = new DockPanel();
            centerPartPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            centerPartPanel.VerticalAlignment = VerticalAlignment.Stretch;
            centerPartPanel.Margin = new Thickness(5);
            centerPartPanel.Children.Add(jobsListGroupBox);
            centerPartPanel.Children.Add(tabControl);
            centerPartPanel.LastChildFill = true;
            DockPanel.SetDock(jobsListGroupBox, Dock.Left);

            var statusBarTextBox = new TextBox();
            statusBarTextBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            statusBarTextBox.VerticalAlignment = VerticalAlignment.Bottom;
            statusBarTextBox.IsEnabled = true;
            statusBarTextBox.IsReadOnly = true;
            statusBarTextBox.Background = new SolidColorBrush(System.Windows.SystemColors.ControlColor);
            statusBarTextBox.Text = Quotes.GetRandomQuote();

            var rootPanel = new DockPanel();
            RootPanel = rootPanel;
            rootPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            rootPanel.VerticalAlignment = VerticalAlignment.Stretch;
            rootPanel.LastChildFill = true;
            AddRootElement(statusBarTextBox);
            AddRootElement(logGroupBox);
            AddRootElement(progressGroupBox);
            AddRootElement(mainMenu);
            AddRootElement(centerPartPanel);
            DockPanel.SetDock(mainMenu, Dock.Top);
            DockPanel.SetDock(centerPartPanel, Dock.Top);
            DockPanel.SetDock(statusBarTextBox, Dock.Bottom);
            DockPanel.SetDock(logGroupBox, Dock.Bottom);
            DockPanel.SetDock(progressGroupBox, Dock.Bottom);
            RootElements.Remove(progressGroupBox); // Only the cancel button can remain active at all times.

            AltListBoxBg.ApplyTo(_jobsListView);
            AltListBoxBg.ApplyTo(log.LogListBox);

            var label = new Label { Content = "You can drag'n'drop files and folders here.", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var brush = new VisualBrush(label) { Stretch = Stretch.None, Opacity = 0.5 };
            _jobsListView.Background = brush;

            CreateWindow();

            LogInfo("UMM version " + UmmVersion.String + " is now operational!");

            ProcessCommandLine(cmdLineArgs);

            RunApplication();
        }
        
        private void ProcessCommandLine(string[] cmdLineArgs)
        {
            var droppedPaths = cmdLineArgs;
            var droppedFilePaths = new List<string>();
            var droppedFolderPaths = new List<string>();

            foreach(var droppedPath in droppedPaths)
            {
                var path = droppedPath;
                if(File.Exists(path) && Path.GetExtension(path).ToLower() == ".lnk")
                {
                    string realPath;
                    if(Shortcut.ResolveShortcut(out realPath, path))
                    {
                        path = realPath;
                    }
                }

                if(File.Exists(path) && IsAviFilePath(path))
                {
                    droppedFilePaths.Add(path);
                }
                else if(Directory.Exists(path))
                {
                    droppedFolderPaths.Add(path);
                }
            }

            AddJobs(droppedFilePaths, droppedFolderPaths);
        }

        private void OnOpenMuxFolder()
        {

        }

        private void OnRemoveFolderClicked()
        {

        }
        
        private void InitFolderListDeleteCommand()
        {
            var inputGesture = new KeyGesture(Key.Delete, ModifierKeys.None);
            var inputBinding = new KeyBinding(_deleteFolderCommand, inputGesture);
            var commandBinding = new CommandBinding();
            commandBinding.Command = _deleteFolderCommand;
            commandBinding.Executed += (obj, args) => OnRemoveFolderClicked();
            commandBinding.CanExecute += (obj, args) => { args.CanExecute = true; };
            _jobsListView.InputBindings.Add(inputBinding);
            _jobsListView.CommandBindings.Add(commandBinding);
        }

        private void OnMuxFolderListBoxDragEnter(object sender, DragEventArgs e)
        {
            bool dataPresent = e.Data.GetDataPresent(DataFormats.FileDrop);

            e.Effects = dataPresent ? DragDropEffects.All : DragDropEffects.None;
        }

        private void OnMuxFolderListBoxDragDrop(object sender, DragEventArgs e)
        {
            var droppedPaths = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            var droppedFilePaths = new List<string>();
            var droppedFolderPaths = new List<string>();

            foreach(var droppedPath in droppedPaths)
            {
                var path = droppedPath;
                if(Path.GetExtension(path).ToLower() == ".lnk")
                {
                    string realPath;
                    if(Shortcut.ResolveShortcut(out realPath, path))
                    {
                        path = realPath;
                    }
                }

                if(File.Exists(path) && IsAviFilePath(path))
                {
                    droppedFilePaths.Add(path);
                }
                else if(Directory.Exists(path))
                {
                    droppedFolderPaths.Add(path);
                }
            }

            AddJobs(droppedFilePaths, droppedFolderPaths);
        }

        private bool IsAviFilePath(string filePath)
        {
            return filePath.ToLower().EndsWith(".avi");
        }

        private class JobAddThreadData
        {
            public readonly List<string> FilePaths = new List<string>();
            public readonly List<string> FolderPaths = new List<string>();
        }

        private void AddJobs(List<string> filePaths, List<string> folderPaths)
        {
            if(filePaths.Count == 0 && folderPaths.Count == 0)
            {
                return;
            }

            Gui_OnJobStart();
            _jobsListView.Background = _jobsListViewBackground;

            if(JobThread != null)
            {
                JobThread.Join();
            }

            // Create a copy to be used only by the thread.
            var data = new JobAddThreadData();
            data.FilePaths.AddRange(filePaths);
            data.FolderPaths.AddRange(folderPaths);

            JobThread = new Thread(JobAddThread);
            JobThread.Start(data);
        }
        
        private void JobAddThread(object arg)
        {
            try
            {
                JobAddThreadImpl(arg);
            }
            catch(Exception exception)
            {
                EntryPoint.RaiseException(exception);
            }
        }

        private void JobAddThreadImpl(object arg)
        {
            var data = arg as JobAddThreadData;
            if(data == null)
            {
                Gui_OnJobEnd();
                return;
            }

            if(data.FilePaths == null || data.FolderPaths == null)
            {
                Gui_OnJobEnd();
                return;
            }

            AddJobsImpl(data.FilePaths, data.FolderPaths);

            Gui_OnJobEnd();
        }

        private void AddJobsImpl(List<string> filePaths, List<string> folderPaths)
        {
            // Single video files.
            foreach(var filePath in filePaths)
            {
                var job = AviSequenceEncodeJob.FromFile(filePath);
                job.Analyze();
                if(!job.IsValid)
                {
                    LogWarning("Invalid file: " + filePath);
                    continue;
                }
                _jobs.Add(job);

                var fileName = Path.GetFileName(filePath);
                var info = new JobDisplayInfo();
                info.Job = job;
                info.Name = fileName;
                info.VideoCount = 1;
                info.FrameCount = job.FrameCount;
                info.HasAudio = job.HasAudio;

                VoidDelegate itemAdder = delegate { _jobsListView.Items.Add(info); };
                _jobsListView.Dispatcher.Invoke(itemAdder);
            }

            // Image sequence(s) folders.
            var rejectedFolderPaths = new List<string>();
            foreach(var folderPath in folderPaths)
            {
                var job = new ImageSequenceEncodeJob(folderPath);
                job.AnalyzeFolder();
                if(!job.IsValid)
                {
                    rejectedFolderPaths.Add(folderPath);
                    continue;
                }
                _jobs.Add(job);

                var folderName = Path.GetFileName(folderPath);
                var info = new JobDisplayInfo();
                info.Job = job;
                info.Name = folderName;
                info.VideoCount = job.SequenceCount;
                info.FrameCount = job.FrameCount;
                info.HasAudio = job.HasAudio;

                VoidDelegate itemAdder = delegate { _jobsListView.Items.Add(info); };
                _jobsListView.Dispatcher.Invoke(itemAdder);
            }

            // Video sequence folders.
            foreach(var folderPath in rejectedFolderPaths)
            {
                var job = AviSequenceEncodeJob.FromFolder(folderPath);
                job.Analyze();
                if(!job.IsValid)
                {
                    LogWarning("Invalid folder: " + folderPath);
                    continue;
                }
                _jobs.Add(job);

                var folderName = Path.GetFileName(folderPath);
                var info = new JobDisplayInfo();
                info.Job = job;
                info.Name = folderName;
                info.VideoCount = 1;
                info.FrameCount = job.FrameCount;
                info.HasAudio = job.HasAudio;

                VoidDelegate itemAdder = delegate { _jobsListView.Items.Add(info); };
                _jobsListView.Dispatcher.Invoke(itemAdder);
            }
        }

        private class JobsProcessThreadData
        {
        }

        private void OnSaveJobsToBatchFile()
        {
            if(_jobs.Count == 0)
            {
                LogWarning("No job in the list.");
                return;
            }

            SaveConfig();

            var saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.InitialDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            saveFileDialog.Filter = "Windows batch file (*.cmd)|*.cmd";
            saveFileDialog.FileName = "UMM_encode_job.cmd";
            var success = saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK;
            saveFileDialog.Dispose();
            if(!success)
            {
                return;
            }

            var fileStream = File.Open(saveFileDialog.FileName, FileMode.Create, FileAccess.Write);
            var streamWriter = new StreamWriter(fileStream);
            foreach(var job in _jobs)
            {
                job.SaveJobToBatchFile(streamWriter);
            }

            streamWriter.Close();
            streamWriter.Dispose();
            fileStream.Close();
            fileStream.Dispose();
        }

        private void OnProcessJobs()
        {
            SaveConfig();

            if(_jobs.Count == 0)
            {
                LogWarning("No job in the list.");
                return;
            }

            if(!IsMEncoderPathValid(Config.MEncoderFilePath))
            {
                return;
            }

            if(_jobs.Count != _jobsListView.Items.Count)
            {
                LogError("The GUI and application job list count don't match. You should restart the application.");
                return;
            }

            Gui_OnJobStart();
            _jobsListView.Background = _jobsListViewBackground;

            if(JobThread != null)
            {
                JobThread.Join();
            }

            // Create a copy to be used only by the thread.
            var data = new JobsProcessThreadData();
            // ...

            JobThread = new Thread(JobsThread);
            JobThread.Start(data);
        }

        private void JobsThread(object arg)
        {
            try
            {
                JobsThreadImpl(arg);
            }
            catch(Exception exception)
            {
                EntryPoint.RaiseException(exception);
            }
        }

        private void JobsThreadImpl(object arg)
        {
            var data = arg as JobsProcessThreadData;
            if(data == null)
            {
                Gui_OnJobEnd();
                return;
            }

            ColorCodecDialogShown = false;
            MonochromeCodecDialogShown = false;

            _currentJobProgress = 0.0;
            SetProgressThreadSafe(0.0);

            TotalWorkLoad = 0;
            for(var i = 0; i < _jobs.Count; ++i)
            {
                TotalWorkLoad += _jobs[i].FrameCount;
            }

            ProcessedWorkLoad = 0;
            for(var i = 0; i < _jobs.Count; ++i)
            {
                if(CancelJobValue == 1)
                {
                    break;
                }

                OnJobStart();

                var job = _jobs[i];
                CurrentJobWorkLoad = job.FrameCount;

                var display = _jobsListView.Items[i] as JobDisplayInfo;
                if(display != null)
                {
                    VoidDelegate displayUpdater = delegate { display.Status = "encoding"; _jobsListView.Items.Refresh(); };
                    _jobsListView.Dispatcher.Invoke(displayUpdater);
                }

                var success = job.ProcessJob();
                if(display != null)
                {
                    VoidDelegate displayUpdater = delegate { display.Status = success ? "success" : "failure"; _jobsListView.Items.Refresh(); };
                    _jobsListView.Dispatcher.Invoke(displayUpdater);
                }

                ProcessedWorkLoad += job.FrameCount;
                var progress = 100.0 * (ProcessedWorkLoad / (double)TotalWorkLoad);

                _currentJobProgress = 0.0;
                SetProgressThreadSafe(progress);
            }

            Gui_OnJobEnd();
        }

        public void LogInfo(string message, params object[] args)
        {
            Log.LogInfo(message, args);
        }

        public void LogWarning(string message, params object[] args)
        {
            Log.LogWarning(message, args);
        }

        public void LogError(string message, params object[] args)
        {
            Log.LogError(message, args);
        }

        private void SetCodecDialogShown(bool monochrome)
        {
            if(monochrome)
            {
                MonochromeCodecDialogShown = true;
            }
            else
            {
                ColorCodecDialogShown = true;
            }
        }

        public void WriteTobatchFile(StreamWriter file, string workingDir, MEncoderArguments args)
        {
            workingDir = Path.GetFullPath(workingDir);
            var arguments = CreateMEncoderArguments(workingDir, args);
            var encoderPath = Path.GetFullPath(Config.MEncoderFilePath);
            var stringBuilder = new StringBuilder();

            // Set the current directory.
            stringBuilder.Append("cd /D \"");
            stringBuilder.Append(workingDir);
            stringBuilder.AppendLine("\"");

            // Invoke MEncoder.
            stringBuilder.Append("\"");
            stringBuilder.Append(encoderPath);
            stringBuilder.Append("\" ");
            stringBuilder.AppendLine(arguments);

            file.Write(stringBuilder.ToString());
        }

        public ProcessStartInfo CreateMEncoderProcessStartInfo(string workingDir, MEncoderArguments args)
        {
            workingDir = Path.GetFullPath(workingDir);

            // @MSDN:
            // You must set UseShellExecute to false if you want to set RedirectStandardOutput to true.
            // Otherwise, reading from the StandardOutput stream throws an exception.
            var info = new ProcessStartInfo();
            info.Arguments = CreateMEncoderArguments(workingDir, args);
            info.CreateNoWindow = true;
            info.ErrorDialog = false;
            info.FileName = Path.GetFullPath(Config.MEncoderFilePath);
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.UseShellExecute = false;
            info.WindowStyle = ProcessWindowStyle.Hidden;
            info.WorkingDirectory = workingDir;

            // @TODO: Remove?
            LogInfo("Processing '{0}'", workingDir);
            LogInfo(info.Arguments);

            return info;
        }

        public string CreateMEncoderArguments(string workingDir, MEncoderArguments args)
        {
            // MEncoder arguments examples:
            // @""new.avi" -audiofile "new.wav" -oac copy -ovc vfw -xvfwopts codec=LAGARITH.DLL -of avi -ofps 60 -o ..\umm_test_output.avi";
            // @"mf://*.tga -mf fps=60 -audiofile "new.wav" -oac copy -ovc vfw -xvfwopts codec=LAGARITH.DLL -of avi -o ..\umm_test_output.avi";

            var codec = args.Monochrome ? Config.MonochromeCodec : Config.ColorCodec;
            var customOptionsLavc = args.Monochrome ? Config.CustomMonochromeOptionsLavc : Config.CustomColorOptionsLavc;
            var customCodecVfw = args.Monochrome ? Config.CustomMonochromeVfwCodecName : Config.CustomColorVfwCodecName;
            var showCodecDialog = args.Monochrome ? Config.ShowMonochromeCodecDialog : Config.ShowColorCodecDialog;
            var codecDialogShown = args.Monochrome ? MonochromeCodecDialogShown : ColorCodecDialogShown;
            if(args.CodecOverride)
            {
                codec = args.Codec;
            }

            if(args.ImageSequence && args.InputImagesPath == null)
            {
                LogWarning("CreateMEncoderArguments: ");
                LogWarning("Told to use an image sequence but none was specified. The format is like this: *.tga");
            }
            if(args.InputVideoPaths.Count > 1 && args.UseSeparateAudioFile)
            {
                LogWarning("CreateMEncoderArguments: args.InputVideoPaths.Count > 1 && args.UseSeparateAudioFile");
                LogWarning("Can't mux multiple .avi files with 1 .wav in MEncoder");
            }
            if(args.UseSeparateAudioFile && (args.InputAudioPath == null || !File.Exists(args.InputAudioPath)))
            {
                LogWarning("CreateMEncoderArguments: args.UseSeparateAudioFile && (args.InputAudioPath == null || !File.Exists(args.InputAudioPath))");
                LogWarning("Told to use an audio file but none valid was specified");
            }

            var arguments = new StringBuilder();
            if(args.ImageSequence)
            {
                arguments.Append(" mf://");
                arguments.Append(args.InputImagesPath);

                arguments.Append(" -mf fps=");
                arguments.Append(Config.FrameRate);
            }
            else
            {
                arguments.Append(" -fps ");
                arguments.Append(Config.FrameRate);

                foreach(var inputVideoPath in args.InputVideoPaths)
                {
                    arguments.Append(" \"");
                    arguments.Append(inputVideoPath);
                    arguments.Append("\"");
                }
            }

            if(Config.FramesToSkip > 0)
            {
                var startTimeSeconds = (double)Config.FramesToSkip * (1.0 / (double)Config.OutputFrameRate);
                arguments.Append(" -ss ");
                arguments.Append(startTimeSeconds);
            }
            
            if(args.UseSeparateAudioFile && args.InputAudioPath != null && File.Exists(args.InputAudioPath))
            {
                var audioFilePath = args.InputAudioPath;
                var audioFileName = Path.GetFileName(audioFilePath);
                if( Path.GetFullPath(audioFilePath).ToLower() == 
                    Path.GetFullPath(Path.Combine(workingDir, audioFileName)).ToLower())
                {
                    audioFilePath = audioFileName;
                }

                arguments.Append(" -audiofile \"");
                arguments.Append(audioFilePath);
                arguments.Append("\" -oac copy");
            }
            else if(!args.ImageSequence && args.InputVideoPaths.Count == 1 && args.AviHasAudio)
            {
                arguments.Append(" -oac copy");
            }

            switch(codec)
            {
                case VideoCodec.Copy:
                    arguments.Append(" -ovc copy");
                    break;

                case VideoCodec.Raw:
                    arguments.Append(" -ovc raw");
                    break;

                case VideoCodec.Lagarith:
                    arguments.Append(" -ovc vfw -xvfwopts codec=LAGARITH.DLL");
                    if(showCodecDialog && !codecDialogShown)
                    {
                        arguments.Append(":compdata=dialog");
                        SetCodecDialogShown(args.Monochrome);
                    }
                    break;

                case VideoCodec.Lavc:
                    arguments.Append(" -ovc lavc");
                    if(!string.IsNullOrWhiteSpace(customOptionsLavc))
                    {
                        arguments.Append(" -lavcopts ");
                        arguments.Append(customOptionsLavc);
                    }
                    break;

                case VideoCodec.CustomVfw:
                    arguments.Append(" -ovc vfw -xvfwopts codec=");
                    arguments.Append(customCodecVfw);
                    if(showCodecDialog && !codecDialogShown)
                    {
                        arguments.Append(":compdata=dialog");
                        SetCodecDialogShown(args.Monochrome);
                    }
                    break;
            }

            if(args.ImageSequence && Config.OutputFrameRate != Config.FrameRate)
            {
                arguments.Append(" -ofps ");
                arguments.Append(Config.OutputFrameRate);
            }

            arguments.Append(" -of avi -o \"");
            arguments.Append(args.OutputFilePath);
            arguments.Append("\"");

            // Strip the leading space.
            arguments.Remove(0, 1);

            return arguments.ToString();
        }

        public string CreateOutputFileName(string sourceFileName)
        {
            var method = Config.FileNamingPolicy;
            if(method == FileNamingMethod.NoChange)
            {
                return sourceFileName;
            }

            if(method == FileNamingMethod.AddPrefix)
            {
                return Config.FileNamingPrefix + sourceFileName;
            }

            if(method == FileNamingMethod.AddSuffix)
            {
                return Path.GetFileNameWithoutExtension(sourceFileName) + Config.FileNamingSuffix + Path.GetExtension(sourceFileName);
            }

            try
            {
                var regExp = new Regex(Config.FileNamingRegExpMatch, RegexOptions.None);

                return regExp.Replace(sourceFileName, Config.FileNamingRegExpReplacement);
            }
            catch(Exception exception)
            {
                LogError(exception.Message);
                LogWarning("Invalid RegExp, using source file name instead");
            }

            return sourceFileName;
        }

        public bool IsMEncoderPathValid(string mEncoderFilePath, bool fileNameSuggestion = false)
        {
            if(!File.Exists(Config.MEncoderFilePath))
            {
                LogError("The selected MEncoder file path doesn't point to an existing file");
                return false;
            }

            if(Path.GetExtension(Config.MEncoderFilePath).ToLower() != ".exe")
            {
                LogError("The selected MEncoder file path doesn't point to an executable (.exe)");
                return false;
            }

            var fileNameLower = Path.GetFileName(mEncoderFilePath).ToLower();
            if(fileNameSuggestion && fileNameLower != "mencoder.exe")
            {
                if(fileNameLower == "mplayer.exe")
                {
                    LogWarning("The selected MEncoder file path points to an executable named \"mplayer.exe\". It is usually right next to \"mencoder.exe\". Was it a misclick?");
                }
                else
                {
                    LogWarning("The selected MEncoder file path doesn't point to an executable named \"mencoder.exe\"");
                }

                return false;
            }

            return true;
        }

        // Called from the main window's thread.
        private void Gui_OnJobStart()
        {
            DisableUiNonThreadSafe();
            ShowProgressNonThreadSafe();
        }

        // Called from the job thread.
        private void Gui_OnJobEnd()
        {
            HideProgressThreadSafe();
            EnableUiThreadSafe();
        }
    }
}