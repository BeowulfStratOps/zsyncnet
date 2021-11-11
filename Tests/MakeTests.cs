using System;
using System.IO;
using NUnit.Framework;
using zsyncnet;

namespace Tests
{
    public class MakeTests
    {
        [Test]
        public void BigFileWeakChecksumLength()
        {
            var data = new byte[400 * 1024 * 1024]; // about the minimum size to get 3 byte weak checksums
            new Random(1).NextBytes(data);

            var controlFile = ZsyncMake.MakeControlFile(new MemoryStream(data), default, null);

            Assert.AreEqual(3, controlFile.GetHeader().WeakChecksumLength);

            var firstRSum = controlFile.GetBlockSums()[0].Rsum;

            Assert.GreaterOrEqual(firstRSum, ushort.MaxValue);
        }
    }
}
