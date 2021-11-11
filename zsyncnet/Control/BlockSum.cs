using System.Collections.Generic;
using System.IO;
using zsyncnet.Util;

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

        private static BlockSum ReadBlockSum(Stream input, int rsumBytes, int checksumBytes, int start)
        {
            var rsum = ReadRsum(input, rsumBytes);
            var checksum = ReadChecksum(input, checksumBytes);
            return new BlockSum(rsum, checksum, start);
        }

        private static uint ReadRsum(Stream input, int bytes)
        {
            var block = new byte[bytes];
            input.Read(block);
            return EndianConverter.FromBigEndian(block);
        }


        private static byte[] ReadChecksum(Stream input, int length)
        {
            var buffer = new byte[length];
            input.Read(buffer);
            return buffer;
        }
    }
}
