using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using NLog;
using zsyncnet.Sync;

[assembly: InternalsVisibleTo("Tests")]

namespace zsyncnet
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Zsync
    {
        private static bool IsAbsoluteUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out _);
        }

        private static ControlFile DownloadControlFile(Uri uri)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = client.Send(request);
            response.EnsureSuccessStatusCode();
            using var stream = response.Content.ReadAsStream();
            return new ControlFile(stream);
        }

        /// <summary>
        /// Syncs a file
        /// </summary>
        /// <param name="zsyncFile"></param>
        /// <param name="output"></param>
        /// <param name="progress"></param>
        /// <exception cref="WebException"></exception>
        /// <exception cref="Exception"></exception>
        public static void Sync(Uri zsyncFile, DirectoryInfo output, IProgress<long> progress = null)
        {
            // Load zsync control file
            var cf = DownloadControlFile(zsyncFile);

            var path = Path.Combine(output.FullName, cf.GetHeader().Filename.Trim());
            if (!File.Exists(path)) throw new FileNotFoundException();

            Uri fileUri;

            if (cf.GetHeader().Url == null || !IsAbsoluteUrl(cf.GetHeader().Url))
            {
                // Relative
                fileUri = new Uri(zsyncFile.ToString().Replace(".zsync", string.Empty));
            }
            else
            {
                fileUri = new Uri(cf.GetHeader().Url);
            }

            var rangeDownloader = new RangeDownloader(fileUri);

            // TODO: use temp path as additional seed file
            var tempPath = new FileInfo(path + ".part");
            Directory.CreateDirectory(tempPath.Directory.FullName);
            using (var tmpStream = new FileStream(tempPath.FullName, FileMode.Create, FileAccess.ReadWrite))
            using (var stream = File.OpenRead(path))
            {
                Sync(cf, stream, rangeDownloader, tmpStream);
            }


            var logger = LogManager.GetCurrentClassLogger();
            logger.Debug($"Downloaded: {rangeDownloader.TotalBytesDownloaded}bytes in {rangeDownloader.RangesDownloaded} requests.");

            File.Move(tempPath.FullName, path, true);
            File.SetLastWriteTime(path, cf.GetHeader().MTime);
        }

        public static void Sync(ControlFile controlFile, Stream seed, IRangeDownloader downloader, Stream output, IProgress<long> progress = null)
        {
            ZsyncPatch.Patch(seed, controlFile, downloader, output);
        }
    }
}
