using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;
using MiscUtil.Conversion;
using MiscUtil.IO;

namespace zsyncnet
{
    public class BlockSum
    {
        protected bool Equals(BlockSum other)
        {
            return Rsum == other.Rsum;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BlockSum) obj);
        }

        public override int GetHashCode()
        {
            return Rsum.GetHashCode();
        }

        public readonly uint Rsum;
        public readonly byte[] Checksum;
        public int BlockStart { get; set; }


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

        private static byte[] Pad(byte[] array, int start, int end, byte value)
        {
            for (int i = start; i < end; i++)
            {
                array[i] = value;
            }

            return array;
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

        public bool ChecksumsMatch(BlockSum other)
        {
            return Rsum == other.Rsum && Checksum.SequenceEqual(other.Checksum);
        }
    }
}
