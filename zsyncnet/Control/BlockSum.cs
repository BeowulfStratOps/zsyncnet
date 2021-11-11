using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiscUtil.Conversion;
using MiscUtil.IO;

namespace zsyncnet.Control
{
    internal class BlockSum
    {
        public readonly uint Rsum;
        public readonly byte[] Checksum;
        public readonly int BlockStart;


        public BlockSum(uint rsum, byte[] checksum, int start)
        {
            Rsum = rsum;
            Checksum = checksum;
            BlockStart = start;
        }

        public static List<BlockSum> ReadBlockSums(byte[] input, int blockCount,  int rsumBytes, int checksumBytes )
        {
            var inputStream = new MemoryStream(input);
            var blocks = new List<BlockSum>(blockCount);
            for (var i = 0; i < blockCount; i++)
            {
                // Read rsum, then read checksum
                blocks.Add(ReadBlockSum(inputStream,rsumBytes,checksumBytes,i));
            }

            return blocks;
        }

        private static BlockSum ReadBlockSum(MemoryStream input, int rsumBytes, int checksumBytes, int start)
        {
            var rsum = ReadRsum(input, rsumBytes);
            var checksum = ReadChecksum(input, checksumBytes);
            return new BlockSum(rsum, checksum, start);
        }

        private static uint ReadRsum(MemoryStream input, int bytes)
        {
            var br = new EndianBinaryReader(EndianBitConverter.Big, input);
            var block = new byte[4];
            for (var i = bytes - 1; i >= 0; i--)
            {
                var next = br.ReadByte();
                block[i] = next;
            }

            return BitConverter.ToUInt32(block);
        }


        private static byte[] ReadChecksum(MemoryStream input, int length)
        {
            var br = new EndianBinaryReader(EndianBitConverter.Big, input);
            var checksum = new byte[length];
            var read = 0;
            int r;
            while (read < length && (r = br.Read(checksum, read, length - read)) != 0)
            {
                read += r;
            }

            return checksum;
        }
    }
}
