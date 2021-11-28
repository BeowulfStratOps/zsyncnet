using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Tests.Util;
using zsyncnet;

namespace Tests
{
    public class PartTests : LoggedTest
    {
        private static void DoTest(byte[] seed, byte[] data, byte[] partFile, int expectedBytesDownloads, int expectedRanges)
        {
            var cf = ZsyncMake.MakeControlFile(new MemoryStream(data), DateTime.Now, "test.bin");
            var downloader = new DummyRangeDownloader(data);

            var output = new MemoryStream(data.Length);
            output.Write(partFile);
            output.Position = 0;
            var seeds = new List<Stream> { new MemoryStream(seed) };
            Zsync.Sync(cf, seeds, downloader, output);

            Assert.AreEqual(data, output.ToArray());
            Assert.AreEqual(expectedBytesDownloads, downloader.TotalBytesDownloaded);
            Assert.AreEqual(expectedRanges, downloader.RangesDownloaded);
        }

        [Test]
        public void SimplePartFileSmall()
        {
            var random = new Random();

            // blocksize will be 2048, with sequence = 2. -> effective blocksize for testing is 4096

            var data = new byte[4 * 4096];
            random.NextBytes(data);

            // seed has block 2 and 3
            var seed = new byte[2 * 4096];
            Array.Copy(data, 1 * 4096, seed, 0, 2 * 4096);

            // part has block 1
            var part = new byte[1 * 4096];
            Array.Copy(data, 0 * 4096, part, 0, 1 * 4096);

            DoTest(seed, data, part, 1 * 4096, 1);
        }

        [Test]
        public void SimplePart()
        {
            var random = new Random();

            var data = new byte[2048 * 2048];
            random.NextBytes(data);

            // seed has block 500 to 1500
            var seed = new byte[1000 * 2048];
            Array.Copy(data, 500 * 2048, seed, 0, 1000 * 2048);

            // part has block 0-500
            var part = new byte[500 * 2048];
            Array.Copy(data, 0, part, 0, 500 * 2048);

            DoTest(seed, data, part, 548 * 2048, 1);
        }

        [Test]
        public void PartFileWithBackwardCopies()
        {
            // we have a 10mb buffer in memory. within that, we can copy from bigger to smaller indices.
            // so for testing, we need to go past those 10mb

            var random = new Random();

            const int mb = 1024 * 1024;

            var data = new byte[20 * mb];
            random.NextBytes(data);

            // seed has mb 5 to 10
            var seed = new byte[5 * mb];
            Array.Copy(data, 5 * mb, seed, 0, 5 * mb);

            // part has 10mb of junk, then block 0 to 5 -> can't use it.
            var part = new byte[15 * mb];
            random.NextBytes(part.AsSpan(0, 10 * mb));
            Array.Copy(data, 0 * mb, part, 10 * mb, 5 * mb);

            DoTest(seed, data, part, 15 * mb, 2);
        }
    }
}
