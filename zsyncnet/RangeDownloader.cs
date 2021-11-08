using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using NLog;

namespace zsyncnet
{
    public class RangeDownloader : IRangeDownloader
    {
        private readonly Uri _fileUri;
        private readonly HttpClient _client = new();
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public long TotalBytesDownloaded { get; private set; }
        public long RangesDownloaded { get; private set; }

        public RangeDownloader(Uri fileUri)
        {
            _fileUri = fileUri;
        }

        public Stream DownloadRange(long from, long to)
        {
            // last index is inclusive in http range
            var range = new RangeHeaderValue(from, to - 1);

            var req = new HttpRequestMessage
            {
                RequestUri = _fileUri,
                Headers = {Range = range}
            };

            var response = _client.SendAsync(req).Result;
            response.EnsureSuccessStatusCode();
            Logger.Trace($"Downloading {range}");
            var stream = response.Content.ReadAsStreamAsync().Result;
            TotalBytesDownloaded += stream.Length;
            RangesDownloaded++;
            return stream;
        }
    }
}
