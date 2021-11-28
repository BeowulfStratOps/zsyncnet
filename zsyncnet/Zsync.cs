using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
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
        /// <param name="cancellationToken"></param>
        /// <exception cref="WebException"></exception>
        /// <exception cref="Exception"></exception>
        public static void Sync(Uri zsyncFile, DirectoryInfo output, IProgress<ulong> progress = null, CancellationToken cancellationToken = default)
        {
            // Load zsync control file
            var cf = DownloadControlFile(zsyncFile);

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

            var downloader = new RangeDownloader(fileUri, new HttpClient());

            Sync(cf, downloader, output, progress, cancellationToken);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="controlFile"></param>
        /// <param name="downloader"></param>
        /// <param name="output"></param>
        /// <param name="progress"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="FileNotFoundException"></exception>
        public static void Sync(ControlFile controlFile, IRangeDownloader downloader, DirectoryInfo output, IProgress<ulong> progress = null, CancellationToken cancellationToken = default)
        {
            var path = Path.Combine(output.FullName, controlFile.GetHeader().Filename.Trim());
            if (!File.Exists(path)) throw new FileNotFoundException();

            var partFile = new FileInfo(path + ".part");

            var tmpStream = new FileStream(partFile.FullName, FileMode.OpenOrCreate, FileAccess.ReadWrite);

            var seeds = new List<Stream> { File.OpenRead(path) };

            try
            {
                Sync(controlFile, seeds, downloader, tmpStream, progress, cancellationToken);
            }
            finally
            {
                tmpStream.Close();
                foreach (var seed in seeds)
                {
                    seed.Close();
                }
            }

            File.Move(partFile.FullName, path, true);
            File.SetLastWriteTime(path, controlFile.GetHeader().MTime);
        }

        /// <summary>
        /// output stream can not be a seed as well.
        /// </summary>
        /// <param name="controlFile"></param>
        /// <param name="seeds"></param>
        /// <param name="downloader"></param>
        /// <param name="output"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="progress"></param>
        public static void Sync(ControlFile controlFile, List<Stream> seeds, IRangeDownloader downloader, Stream output, IProgress<ulong> progress = null, CancellationToken cancellationToken = default)
        {
            ZsyncPatch.Patch(seeds, controlFile, downloader, output, progress, cancellationToken);
        }
    }
}
