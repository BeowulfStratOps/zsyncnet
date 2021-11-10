using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using zsyncnet.Control;
using zsyncnet.Hash;
using zsyncnet.Util;

namespace zsyncnet
{
    public static class ZsyncMake
    {
        private const int BlockSizeSmall = 2048;
        private const int BlockSizeLarge = 4096;
        private const string ZsyncVersion = "0.6.2";

        public static ControlFile MakeControlFile(Stream file, DateTime lastWrite, string name)
        {
            var fileLength = file.Length;

            var blockSize = CalculateBlocksize(file.Length);

            var sequenceMatches = fileLength > blockSize ? 2 : 1;
            var weakChecksumLength = CalculateWeakChecksumLength(fileLength, blockSize, sequenceMatches);
            var strongCheckSumLength = CalculateStrongChecksumLength(fileLength, blockSize, sequenceMatches);

            var checkSums = ComputeCheckSums(file, weakChecksumLength, strongCheckSumLength, blockSize);

            using var crypto = new SHA1CryptoServiceProvider();
            file.Position = 0;
            var sha1 = ZsyncUtil.ByteToHex(crypto.ComputeHash(file));

            var header = new Header(ZsyncVersion, name, lastWrite, blockSize, fileLength, sequenceMatches,
                weakChecksumLength, strongCheckSumLength, null, sha1);

            return new ControlFile(header, checkSums);
        }

        public static ControlFile MakeControlFile(FileInfo file)
        {
            var mtime = File.GetLastWriteTimeUtc(file.FullName);
            using var stream = file.OpenRead();
            var cf = MakeControlFile(stream, mtime, file.Name);
            return cf;
        }

        /// <summary>
        /// Calculates blocksize based on the zsync paper
        /// </summary>
        /// <param name="fileLength"></param>
        /// <returns></returns>
        private static int CalculateBlocksize(long fileLength)
        {
            return fileLength < 100 * 1 << 20 ? BlockSizeSmall : BlockSizeLarge;
        }

        private static int CalculateStrongChecksumLength(long fileLength, int blockSize, int sequenceMatches)
        {
            var d = (Math.Log(fileLength) + Math.Log(1 + fileLength / blockSize)) / Math.Log(2) + 20;

            // reduced number of bits by sequence matches
            var l1 = (int) Math.Ceiling(d / sequenceMatches / 8);

            // second checksum - not reduced by sequence matches
            var l2 = (int) ((Math.Log(1 + fileLength / blockSize) / Math.Log(2) + 20 + 7.9) / 8);

            // return max of two: return no more than 16 bytes (MD4 max)
            return Math.Min(16, Math.Max(l1, l2));
        }

        private static int CalculateWeakChecksumLength(long fileLength, int blockSize, int sequenceMatches)
        {
            double d = (Math.Log(fileLength) + Math.Log(blockSize)) / Math.Log(2) - 8.6;

            // reduced number of bits by sequence matches per http://zsync.moria.org.uk/paper/ch02s04.html
            int l = (int) Math.Ceiling(d / sequenceMatches / 8);

            // enforce max and min values
            return l > 4 ? 4 : l < 2 ? 2 : l;
        }

        private static List<BlockSum> ComputeCheckSums(Stream input, int weakLength, int strongLength, int blockSize)
        {
            var result = new List<BlockSum>();

            var count = 0;
            var block = new byte[blockSize];
            int read;
            while ((read = input.Read(block)) != 0)
            {
                if (read < blockSize)
                {
                    // Pad with 0's
                    block = Pad(block, read, blockSize, 0);
                }

                var weakCheckSum = (ushort)ZsyncUtil.ComputeRsum(block, weakLength);
                var strongCheckSum = new byte[16];
                new Md4(block.Length).Hash(block, 0, strongCheckSum);
                Array.Resize(ref strongCheckSum, strongLength);

                result.Add(new BlockSum(weakCheckSum, strongCheckSum, count));
                count++;
            }

            return result;
        }


        private static byte[] Pad(byte[] array, int start, int end, byte value)
        {
            for (int i = start; i < end; i++)
            {
                array[i] = value;
            }

            return array;
        }
    }
}
