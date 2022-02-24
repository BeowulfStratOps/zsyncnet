using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using zsyncnet;

namespace Tests
{
    public class FileTests
    {
        // TODO: tests are heavily relying on implementation details. fix that.

        private DirectoryInfo _tempPath = null!;
        private FileInfo _targetFile = null!;

        private const int RandomDataLength = 2048 * 5;

        [SetUp]
        public void SetUp()
        {
            var tempDir = new DirectoryInfo(Path.GetTempPath());
            _tempPath = tempDir.CreateSubdirectory(Path.GetRandomFileName());
            var data = GetPartiallyRandomData(123, 456, 789);
            _targetFile = new FileInfo(Path.Combine(_tempPath.FullName, "target.bin"));
            File.WriteAllBytes(_targetFile.FullName, data);
        }

        [TearDown]
        public void TearDown()
        {
            _tempPath.Delete(true);
        }

        private static byte[] GetPartiallyRandomData(int seedA, int seedB, int seedC)
        {
            var data = new byte[3 * RandomDataLength];
            var memStream = new MemoryStream(data);

            foreach (var seed in new []{seedA, seedB, seedC})
            {
                var blob = new byte[RandomDataLength];
                new Random(seed).NextBytes(blob);
                memStream.Write(blob);
            }
            memStream.Close();

            return data;
        }

        private (DummyRangeDownloader downloader, ControlFile cf, byte[] data) BuildRemote(int seedA, int seedB, int seedC)
        {
            var data = GetPartiallyRandomData(seedA, seedB, seedC);
            var downloader = new DummyRangeDownloader(data);
            using var asStream = new MemoryStream(data);
            var cf = ZsyncMake.MakeControlFile(asStream, new DateTime(2000, 5, 6), "target.bin");
            return (downloader, cf, data);
        }

        [Test]
        public void TestSyncing()
        {
            var (downloader, cf, data) = BuildRemote(1, 2, 3);
            Zsync.Sync(cf, downloader, _tempPath);

            Assert.AreEqual(data, File.ReadAllBytes(_targetFile.FullName));
            Assert.AreEqual(new [] { "target.bin" }, _tempPath.EnumerateFiles().Select(fi => fi.Name));
            Assert.AreEqual(3 * RandomDataLength, downloader.TotalBytesDownloaded);
            Assert.AreEqual(1, downloader.RangesDownloaded);
        }

        [Test]
        public void TestSyncingWithPartFile()
        {
            var (downloader, cf, data) = BuildRemote(1, 2, 3);

            var partData = GetPartiallyRandomData(1, 4444, 5555); // first bit is already done, others are random
            File.WriteAllBytes(Path.Join(_tempPath.FullName, "target.bin.part"), partData);

            Zsync.Sync(cf, downloader, _tempPath);

            Assert.AreEqual(data, File.ReadAllBytes(_targetFile.FullName));
            Assert.AreEqual(new [] { "target.bin" }, _tempPath.EnumerateFiles().Select(fi => fi.Name));
            Assert.AreEqual(2 * RandomDataLength, downloader.TotalBytesDownloaded);
            Assert.AreEqual(1, downloader.RangesDownloaded);
        }

        [Test]
        public void TestAbortSyncing()
        {
            // use same middlepart as local file, so that we have two separate downloads
            var (downloader, cf, data) = BuildRemote(1, 456, 3);

            var cts = new CancellationTokenSource();

            var firstRead = true;
            downloader.OnRead += () =>
            {
                if (!firstRead) cts.Cancel();
                firstRead = false;
            };

            Assert.Throws<OperationCanceledException>(() =>
                Zsync.Sync(cf, downloader, _tempPath, null, cts.Token));

            Assert.AreEqual(new [] { "target.bin", "target.bin.part" }, _tempPath.EnumerateFiles().Select(fi => fi.Name).OrderBy(n => n.Length).ToArray());

            // check that the first bit of download was written to the part file
            var partPath = Path.Join(_tempPath.FullName, "target.bin.part");
            Assert.AreEqual(data.AsSpan(0, RandomDataLength).ToArray(),
                File.ReadAllBytes(partPath).AsSpan(0, RandomDataLength).ToArray());

            // just making sure cancellation worked..
            Assert.AreNotEqual(data, File.ReadAllBytes(_targetFile.FullName));

            Assert.AreNotEqual(data, File.ReadAllBytes(partPath));
            Assert.AreEqual(1 * RandomDataLength, downloader.TotalBytesDownloaded);
            Assert.AreEqual(1, downloader.RangesDownloaded);
            Assert.Less(File.ReadAllBytes(partPath).Length, data.Length);
        }

        [Test]
        public void TestAbortSyncingWithPartFile()
        {
            var (downloader, cf, data) = BuildRemote(1, 2, 3);

            var partData = GetPartiallyRandomData(1, 2, 3);
            File.WriteAllBytes(Path.Join(_tempPath.FullName, "target.bin.part"), partData);

            var cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                Zsync.Sync(cf, downloader, _tempPath, null, cts.Token);
            }
            catch (OperationCanceledException)
            {
            }

            Assert.AreEqual(new [] { "target.bin", "target.bin.part" }, _tempPath.EnumerateFiles().Select(fi => fi.Name).OrderBy(n => n.Length).ToArray());
        }
    }
}
