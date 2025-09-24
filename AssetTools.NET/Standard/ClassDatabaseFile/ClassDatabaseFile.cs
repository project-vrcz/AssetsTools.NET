using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET.Compression.Lzma;
using EasyCompressor;
using K4os.Compression.LZ4;

namespace AssetsTools.NET
{
    public class ClassDatabaseFile
    {
        public ClassDatabaseFileHeader Header { get; set; }
        public List<ClassDatabaseType> Classes { get; set; }
        public ClassDatabaseStringTable StringTable { get; set; }
        public List<ushort> CommonStringBufferIndices { get; set; }

        /// <summary>
        /// Read the <see cref="ClassDatabaseFile"/> with the provided reader.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        public void Read(AssetsFileReader reader)
        {
            Header ??= new ClassDatabaseFileHeader();
            Header.Read(reader);

            AssetsFileReader dReader = GetDecompressedReader(reader);
            int classCount = dReader.ReadInt32();
            Classes = new List<ClassDatabaseType>(classCount);
            for (int i = 0; i < classCount; i++)
            {
                ClassDatabaseType type = new ClassDatabaseType();
                type.Read(dReader);
                Classes.Add(type);
            }

            StringTable ??= new ClassDatabaseStringTable();
            StringTable.Read(dReader);

            CommonStringBufferIndices ??= new List<ushort>();
            int size = dReader.ReadInt32();
            for (int i = 0; i < size; i++)
            {
                CommonStringBufferIndices.Add(dReader.ReadUInt16());
            }
        }

        /// <summary>
        /// Write the <see cref="ClassDatabaseFile"/> with the provided writer and compression type.
        /// </summary>
        /// <param name="writer">The writer to use.</param>
        /// <param name="compressionType">The compression method to use.</param>
        public void Write(AssetsFileWriter writer, ClassFileCompressionType compressionType)
        {
            Header.CompressionType = compressionType;

            MemoryStream dStream = new MemoryStream();
            AssetsFileWriter dWriter = new AssetsFileWriter(dStream);
            dWriter.Write(Classes.Count);
            for (int i = 0; i < Classes.Count; i++)
            {
                Classes[i].Write(dWriter);
            }

            StringTable.Write(dWriter);

            dWriter.Write(CommonStringBufferIndices.Count);
            for (int i = 0; i < CommonStringBufferIndices.Count; i++)
            {
                dWriter.Write(CommonStringBufferIndices[i]);
            }

            using MemoryStream cStream = GetCompressedStream(dStream);

            Header.CompressedSize = (int)cStream.Length;
            Header.DecompressedSize = (int)dStream.Length;
            Header.Write(writer);

            cStream.CopyToCompat(writer.BaseStream);
        }

        private AssetsFileReader GetDecompressedReader(AssetsFileReader reader)
        {
            AssetsFileReader newReader = reader;
            if (Header.CompressionType != ClassFileCompressionType.Uncompressed)
            {
                var ms = new MemoryStream();
                if (Header.CompressionType == ClassFileCompressionType.Lz4)
                {
                    var lz4Compressor = new LZ4Compressor();
                    lz4Compressor.Decompress(reader.ReadBytes(Header.CompressedSize), ms);
                }
                else if (Header.CompressionType == ClassFileCompressionType.Lzma)
                {
                    LzmaHelper.Decompress(reader.BaseStream, ms, Header.DecompressedSize, Header.CompressedSize);
                }
                else
                {
                    throw new Exception($"Class database is using invalid compression type {Header.CompressionType}!");
                }

                newReader = new AssetsFileReader(ms);
            }

            return newReader;
        }

        private MemoryStream GetCompressedStream(MemoryStream inStream)
        {
            if (Header.CompressionType != ClassFileCompressionType.Uncompressed)
            {
                if (Header.CompressionType == ClassFileCompressionType.Lz4)
                {
                    var lz4Compressor = new LZ4Compressor(LZ4Level.L12_MAX);
                    var tempMs = new MemoryStream();

                    lz4Compressor.Compress(inStream, tempMs);
                    tempMs.Position = 0;
                    return tempMs;
                }

                if (Header.CompressionType == ClassFileCompressionType.Lzma)
                {
                    var outStream = new MemoryStream();
                    LzmaHelper.Compress(inStream, outStream);
                    outStream.Position = 0;
                    return outStream;
                }

                throw new Exception($"Class database is using invalid compression type {Header.CompressionType}!");
            }

            inStream.Position = 0;
            return inStream;
        }

        /// <summary>
        /// Find a class database type by type ID.
        /// </summary>
        /// <param name="id">The type's type ID to search for.</param>
        /// <returns>The type of that type ID.</returns>
        public ClassDatabaseType FindAssetClassByID(int id)
        {
            // 5.4-
            if (id < 0)
            {
                id = 0x72;
            }

            foreach (ClassDatabaseType type in Classes)
            {
                if (type.ClassId == id)
                    return type;
            }

            return null;
        }

        /// <summary>
        /// Find a class database type by type name.
        /// </summary>
        /// <param name="name">The type's type name to search for.</param>
        /// <returns>The type of that type name.</returns>
        public ClassDatabaseType FindAssetClassByName(string name)
        {
            foreach (ClassDatabaseType type in Classes)
            {
                if (GetString(type.Name) == name)
                    return type;
            }

            return null;
        }

        // for convenience

        /// <summary>
        /// Get a string from the string table.
        /// </summary>
        /// <param name="index">The index of the string in the table.</param>
        /// <returns>The string at that index.</returns>
        public string GetString(ushort index) => StringTable.GetString(index);
    }
}