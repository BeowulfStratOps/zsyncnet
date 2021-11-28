using System;
using System.IO;
using NLog;
using zsyncnet;

namespace Tests
{
    internal class DummyRangeDownloader : IRangeDownloader
    {
        private readonly byte[] _data;
        public long TotalBytesDownloaded { get; private set; }
        public long RangesDownloaded { get; private set; }
        public event Action? OnDownload;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public DummyRangeDownloader(byte[] data)
        {
            _data = data;
        }

        public Stream DownloadRange(long @from, long to)
        {
            _logger.Trace($"Downloading range {from} to {to}");
            var stream = new MemoryStream(_data, (int)from, (int)(to - from));
            TotalBytesDownloaded += to - from;
            RangesDownloaded++;

            OnDownload?.Invoke();

            return stream;
        }
    }
}
