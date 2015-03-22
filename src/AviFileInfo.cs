using System;
using System.IO;
using System.Runtime.InteropServices;


namespace Uber
{
    public class AviFileInfo
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct AVIMAINHEADER
        {
            public Int32 fcc; // Must be 'avih'.
            public Int32 cb; // Specifies the size of the structure, not including the initial 8 bytes.
            public Int32 dwMicroSecPerFrame;
            public Int32 dwMaxBytesPerSec;
            public Int32 dwPaddingGranularity;
            public Int32 dwFlags;
            public Int32 dwTotalFrames;
            public Int32 dwInitialFrames;
            public Int32 dwStreams;
            public Int32 dwSuggestedBufferSize;
            public Int32 dwWidth;
            public Int32 dwHeight;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public Int32[] dwReserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct AVISTREAMHEADER
        {
            public Int32 fcc; // Must be 'strh'.
            public Int32 cb; // Specifies the size of the structure, not including the initial 8 bytes.
            public Int32 fccType; // 'auds' 'mids' 'txts' 'vids'
            public Int32 fccHandler;
            public Int32 dwFlags;
            public Int16 wPriority;
            public Int16 wLanguage;
            public Int32 dwInitialFrames;
            public Int32 dwScale;
            public Int32 dwRate;
            public Int32 dwStart;
            public Int32 dwLength;
            public Int32 dwSuggestedBufferSize;
            public Int32 dwQuality;
            public Int32 dwSampleSize;
            public Int16 rcFrameLeft;
            public Int16 rcFrameTop;
            public Int16 rcFrameRight;
            public Int16 rcFrameBottom;
        }

        private static readonly Int32 FOURCC_RIFF = MakeFOURCC('R', 'I', 'F', 'F');
        private static readonly Int32 FOURCC_AVI = MakeFOURCC('A', 'V', 'I', ' ');
        private static readonly Int32 FOURCC_LIST = MakeFOURCC('L', 'I', 'S', 'T');
        private static readonly Int32 FOURCC_MainHeader = MakeFOURCC('a', 'v', 'i', 'h');
        private static readonly Int32 FOURCC_StreamHeader = MakeFOURCC('s', 't', 'r', 'h');
        private static readonly Int32 FOURCC_VideoStream = MakeFOURCC('v', 'i', 'd', 's');
        private static readonly Int32 FOURCC_AudioStream = MakeFOURCC('a', 'u', 'd', 's');
        private static readonly Int32 RIFF_Header = MakeFOURCC('h', 'd', 'r', 'l');
        private static readonly Int32 RIFF_Stream = MakeFOURCC('s', 't', 'r', 'l');

        public bool IsValid
        {
            get;
            private set;
        }

        public int VideoStreams
        {
            get;
            private set;
        }

        public int AudioStreams
        {
            get;
            private set;
        }

        public int FrameCount
        {
            get;
            private set;
        }

        public AviFileInfo(string filePath)
        {
            IsValid = false;
            VideoStreams = -1;
            AudioStreams = -1;
            FrameCount = -1;

            using(var file = File.OpenRead(filePath))
            {
                using(var reader = new BinaryReader(file))
                {
                    var riff = reader.ReadInt32();
                    if(riff != FOURCC_RIFF)
                    {
                        return;
                    }

                    file.Seek(4, SeekOrigin.Current);
                    var avi = reader.ReadInt32();
                    if(avi != FOURCC_AVI)
                    {
                        return;
                    }

                    Int32 listByteSize = 0;
                    Int32 listType = 0;
                    long listOffset = 0;
                    if(!ReadListHeader(file, reader, ref listOffset, ref listByteSize, ref listType))
                    {
                        return;
                    }
                    if(listType != RIFF_Header)
                    {
                        return;
                    }
                    if(!ReadMainHeader(file, reader))
                    {
                        return;
                    }

                    VideoStreams = 0;
                    AudioStreams = 0;

                    var streamCount = 0;
                    while(ReadListHeader(file, reader, ref listOffset, ref listByteSize, ref listType))
                    {
                        if(listType == RIFF_Stream)
                        {
                            if(!ReadStreamHeader(file, reader))
                            {
                                break;
                            }

                            streamCount++;
                        }

                        file.Position = listOffset + listByteSize + 8;
                    }

                    IsValid = streamCount > 0;
                }
            }
        }

        private bool ReadListHeader(FileStream file, BinaryReader reader, ref long offset, ref Int32 byteSize, ref Int32 listType)
        {
            offset = file.Position;

            var list = reader.ReadInt32();
            if(list != FOURCC_LIST)
            {
                return false;
            }

            byteSize = reader.ReadInt32();
            listType = reader.ReadInt32();

            return true;
        }

        private bool ReadMainHeader(FileStream file, BinaryReader reader)
        {
            var offset = file.Position;
            var mainHeader = Read<AVIMAINHEADER>(reader);
            if(mainHeader.fcc != FOURCC_MainHeader)
            {
                return false;
            }

            FrameCount = mainHeader.dwTotalFrames;

            return true;
        }

        private bool ReadStreamHeader(FileStream file, BinaryReader reader)
        {
            var offset = file.Position;
            var streamHeader = Read<AVISTREAMHEADER>(reader);
            if(streamHeader.fcc != FOURCC_StreamHeader)
            {
                Console.WriteLine("ReadStreamHeader :( " + offset);
                return false;
            }

            AudioStreams += streamHeader.fccType == FOURCC_AudioStream ? 1 : 0;
            VideoStreams += streamHeader.fccType == FOURCC_VideoStream ? 1 : 0;

            return true;
        }

        private static T Read<T>(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            var theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();

            return theStructure;
        }

        private static Int32 MakeFOURCC(char ch0, char ch1, char ch2, char ch3)
        {
            return ((Int32)(byte)(ch0) | ((byte)(ch1) << 8) | ((byte)(ch2) << 16) | ((byte)(ch3) << 24));
        }
    }
}