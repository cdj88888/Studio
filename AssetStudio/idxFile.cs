using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
 
//Thank's Ekey   https://github.com/Ekey/ER.DATA.Tool
//If you want to obtain EarthRevival.dll, please click https://github.com/Ekey/ER.DATA.Tool/tree/main/ER.Unpacker/ER.Unpacker/Libs  Download  UnityPlayer.dll And change the name EarthRevival.dll

namespace AssetStudio
{

    public class idxFile
    {

        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string lpFileName);

        private static string ModuleName = "EarthRevival.dll";
        private delegate long JNTE_ZSTDDecompress(byte[] decompressedBuf, uint decompressedSize, byte[] compressedBuf, uint compressedSize);
        private delegate long JNTE_LZ4Decompress(byte[] compressedBuf, byte[] decompressedBuf, uint compressedSize, uint decompressedSize);

        private static JNTE_ZSTDDecompress _ZSTDDecompress;
        private static JNTE_LZ4Decompress _LZ4Decompress;

        public static void Decompress(byte[] compressedBuf, byte[] decompressedBuf, uint compressedSize, uint decompressedSize, int bCompressionType)
        {
            if (_ZSTDDecompress == null || _LZ4Decompress == null)
            {
                var module = LoadLibrary(ModuleName);
                IntPtr function_raw1 = module + 0x10972F0;
                _ZSTDDecompress = Marshal.GetDelegateForFunctionPointer<JNTE_ZSTDDecompress>(function_raw1);

                IntPtr function_raw2 = module + 0x108D160;
                _LZ4Decompress = Marshal.GetDelegateForFunctionPointer<JNTE_LZ4Decompress>(function_raw2);
            }
            if (bCompressionType == 1) {
                _LZ4Decompress(compressedBuf, decompressedBuf, compressedSize, decompressedSize);
            }
            else if (bCompressionType == 3)
            {
                _ZSTDDecompress(decompressedBuf, decompressedSize, compressedBuf, compressedSize);
            }
        }

        public static Byte[] iDecompress(Byte[] lpBuffer, Int32 dwOffset = 0)
        {

            MemoryStream TMemoryStream = new MemoryStream(lpBuffer);
            var MetaInfoReader = new EndianBinaryReader(TMemoryStream);
            MetaInfoReader.Position = dwOffset;
            MetaInfoReader.Endian = EndianType.BigEndian;
            var dwMagic = MetaInfoReader.ReadUInt32();
            if (dwMagic == 0x4A4E5445) // JNTE
            {
                var dwTotalBlocks = MetaInfoReader.ReadInt32();
                var PosIndex = dwOffset + 8;
                var PosData = dwOffset + 8 + dwTotalBlocks * 9;
                var lpResult = new Byte[] { };
                var dwTotalSize = 0;

                for (Int32 i = 0; i < dwTotalBlocks; i++)
                {
                    MetaInfoReader.Position = PosIndex;
                    var bCompressionType = MetaInfoReader.ReadByte();
                    var dwDecompressedSize = MetaInfoReader.ReadInt32();
                    var dwCompressedSize = MetaInfoReader.ReadInt32();
                    PosIndex = PosIndex + 9;

                    MetaInfoReader.Position = PosData;
                    var lpDecompressedBlock = new Byte[dwDecompressedSize];
                    var lpCompressedBlock = MetaInfoReader.ReadBytes(dwCompressedSize);
                    PosData = PosData + dwCompressedSize;

                    dwTotalSize += dwDecompressedSize;
                    Array.Resize(ref lpResult, dwTotalSize);
                    if (bCompressionType == 1 || bCompressionType == 3)
                    {
                        Decompress(lpCompressedBlock, lpDecompressedBlock,(uint)dwCompressedSize,(uint)dwDecompressedSize, bCompressionType); 
                        Array.Copy(lpDecompressedBlock, 0, lpResult, dwOffset, dwDecompressedSize);
                        dwOffset += dwDecompressedSize;
                    }
                    else {
                        Array.Copy(lpCompressedBlock, 0, lpResult, dwOffset, dwCompressedSize);
                        dwOffset += dwCompressedSize;
                    }

                }
                return lpResult;
            }
            return lpBuffer;
        }

        public class EntryTable
        {
            public Int64 m_Hash1;
            public Int64 m_Hash2;
            public uint dwOffset;
            public int dwCompressedSize;
            public int dwDecompressedSize;
            public EntryTable(FileReader reader)
            {
                m_Hash1 = reader.ReadInt64();
                m_Hash2 = reader.ReadInt64();
                dwOffset = reader.ReadUInt32();
                dwCompressedSize = reader.ReadInt32();
                dwDecompressedSize = reader.ReadInt32();
            }
        }


        public List<StreamFile> fileList;
        private List<EntryTable> m_Entry;

        public idxFile(FileReader reader)
        {
            reader.Endian = EndianType.BigEndian;
            var dwTotalContents = reader.ReadInt32();
            var dwTotalFiles = reader.ReadInt32();

            m_Entry = new List<EntryTable>();
            for (Int32 i = 0; i < dwTotalFiles; i++)
            {
                m_Entry.Add(new EntryTable(reader));
            }

            fileList = new List<StreamFile>();
            var directoryPath = Path.GetDirectoryName(reader.FullPath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(reader.FullPath);
            var fullPathWithoutExtension = Path.Combine(directoryPath, fileNameWithoutExtension);
            var path = fullPathWithoutExtension + ".data";
            var Stream = new FileReader(path);

            Logger.Info($"Loading 文件列表和内容，此过程持续时间会比较长，请稍后");

            for (Int32 i = 0; i < dwTotalContents; i++)
            {
                var dwHashName = reader.ReadUInt64();
                var m_Hash1 = reader.ReadInt64();
                var m_Hash2 = reader.ReadInt64();
                var File = m_Entry.Find(x => x.m_Hash1 == m_Hash1 && x.m_Hash2 == m_Hash2);
                if (File != null)
                {
                    Stream.Position = File.dwOffset;
                    var data = Stream.ReadBytes(File.dwCompressedSize);
                    data = iDecompress(data);

                    var file = new StreamFile();
                    file.fileName = dwHashName.ToString("X8");
                    file.path = dwHashName.ToString("X8");
                    file.stream = new MemoryStream();
                    file.stream.Write(data);
                    file.stream.Position = 0;
                    fileList.Add(file);
                }
                else {
                    Logger.Info($"文件信息未找到");
                }
            }

            Stream.Close();
            Stream.Dispose();
        }
    }
}
