using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using NLog;

namespace zsyncnet.Sync
{
    /// <summary>
    /// Downloader for a single remote file.
    /// </summary>
    public class RangeDownloader : IRangeDownloader
    {
        private readonly Uri _fileUri;
        private readonly HttpClient _client;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Creates a Downloader for a specific uri, using the given client.
        /// </summary>
        /// <param name="fileUri">The uri used in requests.</param>
        /// <param name="client">The http client that will be used for requests.</param>
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
