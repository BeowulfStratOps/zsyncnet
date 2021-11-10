using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using NLog;

namespace zsyncnet.Sync
{
    internal class RangeDownloader : IRangeDownloader
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

            Logger.Trace($"Downloading {range}");
            TotalBytesDownloaded += to - from;
            RangesDownloaded++;

            var response = _client.Send(req, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode != HttpStatusCode.PartialContent) throw new HttpRequestException();
            return response.Content.ReadAsStream();
        }
    }
}
