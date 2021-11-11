using System;
using System.IO;
using System.Linq;
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

        [Test]
        public void TestDeSerialisation()
        {
            var data = new byte[1 * 1024 * 1024];
            new Random(1).NextBytes(data);

            var controlFile = ZsyncMake.MakeControlFile(new MemoryStream(data), default, null);
            using var memStream = new MemoryStream();
            controlFile.WriteToStream(memStream);
            memStream.Position = 0;
            var cf2 = new ControlFile(memStream);

            var blockSums = controlFile.GetBlockSums().ToList();
            var blockSums2 = cf2.GetBlockSums().ToList();

            Assert.AreEqual(blockSums.Count, blockSums2.Count);

            for (int i = 0; i < blockSums.Count; i++)
            {
                Assert.AreEqual(blockSums[i].Rsum, blockSums2[i].Rsum);
                Assert.AreEqual(blockSums[i].Checksum, blockSums2[i].Checksum);
                Assert.AreEqual(blockSums[i].BlockStart, blockSums2[i].BlockStart);
            }
        }

        [Test]
        public void TestDeSerialisationBigFile()
        {
            var data = new byte[400 * 1024 * 1024]; // about the minimum size to get 3 byte weak checksums
            new Random(1).NextBytes(data);

            var controlFile = ZsyncMake.MakeControlFile(new MemoryStream(data), default, null);
            using var memStream = new MemoryStream();
            controlFile.WriteToStream(memStream);
            memStream.Position = 0;
            var cf2 = new ControlFile(memStream);

            var blockSums = controlFile.GetBlockSums().ToList();
            var blockSums2 = cf2.GetBlockSums().ToList();

            Assert.AreEqual(blockSums.Count, blockSums2.Count);

            for (int i = 0; i < blockSums.Count; i++)
            {
                Assert.AreEqual(blockSums[i].Rsum, blockSums2[i].Rsum);
                Assert.AreEqual(blockSums[i].Checksum, blockSums2[i].Checksum);
                Assert.AreEqual(blockSums[i].BlockStart, blockSums2[i].BlockStart);
            }
        }
    }
}
