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
        public void SimplePartFile()
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
            var random = new Random();

            var data = new byte[2048 * 2048];
            random.NextBytes(data);

            // seed has block 500 to 1000
            var seed = new byte[500 * 2048];
            Array.Copy(data, 500 * 2048, seed, 0, 500 * 2048);

            // part has block 1000 to 1500 -> can't use them.
            var part = new byte[500 * 2048];
            Array.Copy(data, 1000 * 2048, part, 0, 500 * 2048);

            DoTest(seed, data, part, 1548 * 2048, 2);
        }

        [Test]
        public void PartFileWithBackwardCopiesSmall()
        {
            var random = new Random();

            // blocksize will be 2048, with sequence = 2. -> effective blocksize for testing is 4096

            var data = new byte[4 * 4096];
            random.NextBytes(data);

            // seed has block 2
            var seed = new byte[1 * 4096];
            Array.Copy(data, 1 * 4096, seed, 0, 1 * 4096);

            // part has block 3 -> can't use it.
            var part = new byte[1 * 4096];
            Array.Copy(data, 2 * 4096, part, 0, 1 * 4096);

            DoTest(seed, data, part, 3 * 4096, 2);
        }
    }
}
