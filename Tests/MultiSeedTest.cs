using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Tests.Util;
using zsyncnet;

namespace Tests
{
    public class MultiSeedTest : LoggedTest
    {
        [Test]
        public void TestMultipleSeeds()
        {
            var data = new byte[2048 * 4];
            var random = new Random();
            random.NextBytes(data);

            var seed1 = new byte[2048 * 2];
            Array.Copy(data, 0, seed1, 0, 2048 * 2);
            var seed2 = new byte[2048 * 2];
            Array.Copy(data, 2048 * 2, seed2, 0, 2048 * 2);

            var cf = ZsyncMake.MakeControlFile(new MemoryStream(data), DateTime.Now, "test.bin");
            var downloader = new DummyRangeDownloader(data);

            var output = new MemoryStream(data.Length);
            var seeds = new List<Stream> { new MemoryStream(seed1), new MemoryStream(seed2) };
            Zsync.Sync(cf, seeds, downloader, output);

            Assert.AreEqual(data, output.ToArray());
            Assert.AreEqual(0, downloader.TotalBytesDownloaded);
        }
    }
}
