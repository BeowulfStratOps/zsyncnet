using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NLog;
using zsyncnet.Control;
using zsyncnet.Util;

namespace zsyncnet
{
    /// <summary>
    ///
    /// </summary>
    public class ControlFile
    {
        private readonly ControlFileHeader _header;
        private readonly List<BlockSum> _blockSums;

        internal ControlFile(ControlFileHeader header, List<BlockSum> blockSums)
        {
            _header = header;
            _blockSums = blockSums;
        }

        /// <summary>
        /// Reads a control file from a stream
        /// </summary>
        /// <param name="stream">The control file's data.</param>
        public ControlFile(Stream stream)
        {
            // Read stream in (could be from any source)

            // TODO: use streams all the way
            var (first, last) = SplitFileRead(stream.ToByteArray());

            _header = new ControlFileHeader(first);
            _blockSums = BlockSum.ReadBlockSums(last, _header.GetNumberOfBlocks(), _header.WeakChecksumLength,
                _header.StrongChecksumLength);
            LogManager.GetCurrentClassLogger().Debug($"Total blocks for {_header.Filename}: {_blockSums.Count}, expected {_header.GetNumberOfBlocks()}");
            if (_header.GetNumberOfBlocks() != _blockSums.Count)
            {
                throw new Exception();
            }
        }

        public ControlFileHeader GetHeader()
        {
            return _header;
        }

        internal IReadOnlyList<BlockSum> GetBlockSums()
        {
            return _blockSums;
        }

        private static (byte[] first, byte[] last) SplitFileRead(byte[] file)
        {
            var pos = file.Locate(new byte[] {0x0A, 0x0A});

            var offset = pos[0];

            // Two bytes are ignored, they are the two 0x0A's splitting the file

            byte[] first = new byte[offset];
            byte[] last = new byte[file.Length - offset - 2];

            Array.Copy(file, first, offset);
            Array.Copy(file, offset + 2, last, 0, file.Length - offset - 2);

            return (first, last);
        }

        /// <summary>
        /// Serializes the control file to a file.
        /// </summary>
        /// <param name="path">File to write to.</param>
        public void WriteToFile(string path)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            WriteToStream(fs);
        }

        /// <summary>
        /// Serializes the control file to a stream
        /// </summary>
        /// <param name="stream">target data stream</param>
        public void WriteToStream(Stream stream)
        {
            stream.Write(StringToBytes(BuildHeaderLine("zsync",_header.Version)));
            stream.Write(StringToBytes(BuildHeaderLine("Filename",_header.Filename)));
            stream.Write(
                StringToBytes(BuildHeaderLine("MTime", _header.MTime.ToString("r"))));
            stream.Write(StringToBytes(BuildHeaderLine("Blocksize",_header.BlockSize.ToString())));
            stream.Write(StringToBytes(BuildHeaderLine("Length",_header.Length.ToString())));
            stream.Write(StringToBytes(BuildHeaderLine("Hash-Lengths",$"{_header.SequenceMatches},{_header.WeakChecksumLength},{_header.StrongChecksumLength}")));
            stream.Write(_header.Url != null
                ? StringToBytes(BuildHeaderLine("URL", _header.Url))
                : StringToBytes(BuildHeaderLine("URL", _header.Filename)));
            stream.Write(StringToBytes(BuildHeaderLine("SHA-1", _header.Sha1)));
            stream.Write(StringToBytes("\n"));

            WriteChecksums(stream, _blockSums, _header.WeakChecksumLength);
        }

        private static void WriteChecksums(Stream stream, List<BlockSum> blockSums, int weakLength)
        {
            foreach (var blockSum in blockSums)
            {
                var weakChecksum = EndianConverter.ToBigEndian(blockSum.Rsum, weakLength);
                stream.Write(weakChecksum);
                stream.Write(blockSum.Checksum);
            }
        }

        private static string BuildHeaderLine(string key, string value)
        {
            return $"{key}: {value} \n";
        }

        private static byte[] StringToBytes(string str)
        {
            return Encoding.ASCII.GetBytes(str);
        }

    }
}
