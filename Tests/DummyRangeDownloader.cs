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
        public event Action? OnRead;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public DummyRangeDownloader(byte[] data)
        {
            _data = data;
        }

        public Stream DownloadRange(long @from, long to)
        {
            _logger.Trace($"Downloading range {from} count {to}");
            var stream = new MemoryStreamWithEvents(_data, (int)from, (int)(to - from));

            stream.OnRead += () => OnRead?.Invoke();

            TotalBytesDownloaded += to - from;
            RangesDownloaded++;

            OnDownload?.Invoke();

            return stream;
        }

        public Stream Download()
        {
            _logger.Trace("Downloading entire file");
            var stream = new MemoryStreamWithEvents(_data);
            OnDownload?.Invoke();

            return stream;
        }
    }


    internal class MemoryStreamWithEvents : MemoryStream
    {
        public MemoryStreamWithEvents(byte[] data) : base(data)
        {
        }

        public MemoryStreamWithEvents(byte[] data, int index, int count) : base(data, index, count)
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var result = base.Read(buffer, offset, count);
            OnRead?.Invoke();
            return result;
        }

        public event Action? OnRead;
    }
}
