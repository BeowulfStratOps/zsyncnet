using System;
using System.Collections.Generic;
using NUnit.Framework;
using zsyncnet.Hash;
using zsyncnet.Util;

namespace Tests
{
    public class RollingChecksumTests
    {
        [Test]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Test(int checksumLength)
        {
            const int blockSize = 2048;
            var data = new byte[4096];
            new Random(123).NextBytes(data);
            var rSum = new RollingChecksum(data, blockSize, checksumLength);

            var fromRolling = new List<uint> { rSum.Current };
            for (int i = 0; i < 2048; i++)
            {
                rSum.Next();
                fromRolling.Add(rSum.Current);
            }
            var expected = new List<uint>();

            for (int i = 0; i < data.Length - blockSize + 1; i++)
            {
                var block = new byte[blockSize];
                Array.Copy(data, i, block, 0, blockSize);
                expected.Add(ZsyncRSum.ComputeRsum(block, checksumLength));
            }

            Assert.AreEqual(expected, fromRolling);
        }

    }
}
