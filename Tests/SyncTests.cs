using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Tests.Util;
using zsyncnet;

namespace Tests
{
    [Parallelizable(ParallelScope.Children)]
    public class SyncTests : LoggedTest
    {
        private static void DoTest(byte[] seed, byte[] data, int expectedBytesDownloads, int expectedRanges)
        {
            var cf = ZsyncMake.MakeControlFile(new MemoryStream(data), DateTime.Now, "test.bin");
            var downloader = new DummyRangeDownloader(data);

            var output = new MemoryStream(data.Length);
            var seeds = new List<Stream> { new MemoryStream(seed) };

            var progress = new SynchronousProgress<ulong>();
            ulong totalDone = 0;
            progress.ProgressChanged += p => totalDone += p;

            Zsync.Sync(cf, seeds, downloader, output, progress);

            Assert.AreEqual(data.Length, totalDone);

            Assert.AreEqual(data, output.ToArray());
            Assert.AreEqual(expectedBytesDownloads, downloader.TotalBytesDownloaded);
            Assert.AreEqual(expectedRanges, downloader.RangesDownloaded);
        }

        private static void DoTestFullDownload(byte[] data, int expectedBytesDownloads)
        {
            var cf = ZsyncMake.MakeControlFile(new MemoryStream(data), DateTime.Now, "test.bin");
            var downloader = new DummyRangeDownloader(data);

            var output = new MemoryStream(data.Length);
            var seeds = new List<Stream> { };

            var progress = new SynchronousProgress<ulong>();
            ulong totalDone = 0;
            progress.ProgressChanged += p => totalDone += p;

            Zsync.Sync(cf, seeds, downloader, output, progress);

            Assert.AreEqual(data.Length, totalDone);

            Assert.AreEqual(data, output.ToArray());
            Assert.AreEqual(expectedBytesDownloads, downloader.TotalBytesDownloaded);
        }

        [Test]
        public void NoChange()
        {
            var random = new Random();

            var data = new byte[2048 * 2048];
            random.NextBytes(data);

            var seed = new byte[data.Length];
            data.CopyTo(seed, 0);

            DoTest(seed, data, 0, 0);
        }

        [Test]
        public void SimpleChange()
        {
            var random = new Random();

            var data = new byte[2048 * 2048];
            random.NextBytes(data);

            var seed = new byte[data.Length];
            data.CopyTo(seed, 0);
            seed[0] += 128;

            DoTest(seed, data, 2048, 1);
        }

        [Test]
        public void DuplicateBlock()
        {
            var random = new Random(5);

            var data = new byte[4096];
            random.NextBytes(data);
            for (var i = 0; i < 2048; i++)
            {
                data[i + 2048] = data[i];
            }

            var seed = new byte[data.Length];
            data.CopyTo(seed, 0);

            DoTest(seed, data, 0, 0);
        }

        [Test]
        public void CompleteChange()
        {
            var random = new Random();

            var data = new byte[2048 * 2048];
            random.NextBytes(data);

            var seed = new byte[data.Length];
            for (var i = 0; i < data.Length; i++)
            {
                seed[i] = (byte)(data[i] + 128);
            }

            DoTest(seed, data, data.Length, 1);
        }

        [Test]
        public void LastByteChange()
        {
            var random = new Random();

            var data = new byte[2048];
            random.NextBytes(data);

            var seed = new byte[data.Length];
            data.CopyTo(seed, 0);
            seed[2047] += 128;

            DoTest(seed, data, data.Length, 1);
        }

        [Test]
        public void Padding()
        {
            var random = new Random();

            var data = new byte[3456];
            random.NextBytes(data);

            var seed = new byte[data.Length];
            random.NextBytes(seed);

            DoTest(seed, data, data.Length, 1);
        }

        [Test]
        public void MatchingPaddedBlock()
        {
            var random = new Random();

            var data = new byte[2048+3456];
            random.NextBytes(data);

            var seed = new byte[data.Length];
            random.NextBytes(seed);
            for (int i = 2048; i < data.Length; i++)
            {
                seed[i] = data[i];
            }

            DoTest(seed, data, 2048, 1);
        }

        [Test]
        public void EmptyRemote()
        {
            var random = new Random();

            var data = Array.Empty<byte>();
            random.NextBytes(data);

            var seed = new byte[12345];
            random.NextBytes(seed);

            DoTest(seed, data, 0, 0);
        }

        [Test]
        public void EmptyLocal()
        {
            var random = new Random();

            var data = new byte[12 * 2048 + 1234];
            random.NextBytes(data);

            var seed = Array.Empty<byte>();
            random.NextBytes(seed);

            DoTest(seed, data, data.Length, 1);
        }

        [Test]
        public void AddedByteAtStart()
        {
            var random = new Random();

            var data = new byte[2048 * 2048];
            random.NextBytes(data);

            var seed = new byte[data.Length + 1];
            data.CopyTo(seed, 1);
            seed[0] = (byte)(data[1] + 128);

            DoTest(seed, data, 0, 0);
        }

        [Test]
        public void RemovedByteAtStart()
        {
            var random = new Random();

            var data = new byte[2048 * 2048];
            random.NextBytes(data);

            var seed = new byte[data.Length - 1];
            for (int i = 0; i < data.Length - 1; i++)
            {
                seed[i] = data[i + 1];
            }

            DoTest(seed, data, 2048, 1);
        }

        [Test]
        public void FulLDownload()
        {
            var random = new Random();
            
            var data = new byte[2048 * 2048];
            random.NextBytes(data);

            DoTestFullDownload(data, 4194304);
        }
    }
}
