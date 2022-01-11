using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using NLog;

namespace zsyncnet.Sync
{
    public class RangeDownloader : IRangeDownloader
    {
        private readonly Uri _fileUri;
        private readonly HttpClient _client;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public RangeDownloader(Uri fileUri, HttpClient client)
        {
            _fileUri = fileUri;
            _client = client;
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

            var response = _client.Send(req, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode != HttpStatusCode.PartialContent) throw new HttpRequestException();
            return response.Content.ReadAsStream();
        }

        public Stream Download()
        {
            var response = _client.GetStreamAsync(_fileUri);
            if (!response.IsCompletedSuccessfully) throw new HttpRequestException();
            return response.ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
        }
    }
}
