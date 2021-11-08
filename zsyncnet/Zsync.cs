using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Flurl.Http;
using MiscUtil.Collections.Extensions;
using NLog;
using zsyncnet.Internal;
using zsyncnet.Internal.ControlFile;

namespace zsyncnet
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Zsync
    {
        private static bool IsAbsoluteUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out _);
        }

        /// <summary>
        /// Syncs a file
        /// </summary>
        /// <param name="zsyncFile"></param>
        /// <param name="output"></param>
        /// <returns>Number of bytes downloaded</returns>
        /// <exception cref="WebException"></exception>
        /// <exception cref="Exception"></exception>
        public static long Sync(Uri zsyncFile, DirectoryInfo output)
        {
            // Load zsync control file

            var cf = new ControlFile(zsyncFile.ToString().GetStreamAsync().Result);
            var path = Path.Combine(output.FullName, cf.GetHeader().Filename.TrimStart());

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

            if (fileUri.ToString().HeadAsync().Result.StatusCode == HttpStatusCode.NotFound)
            {
                // File not found
                throw new WebException("File not found");
            }

            if (File.Exists(path))
            {
                // File exists, use the existing file as the seed file

                var rangeDownloader = new RangeDownloader(fileUri);

                var tempPath = new FileInfo(path + ".part");
                Directory.CreateDirectory(tempPath.Directory.FullName);
                using var tmpStream = new FileStream(tempPath.FullName, FileMode.Create, FileAccess.ReadWrite);

                using var stream = File.OpenRead(path);
                Sync(cf, stream, rangeDownloader, tmpStream);

                // TODO: File.SetLastWriteTimeUtc(TempPath.FullName, _mtime);
                // TODO: replace file with tmpfile

                var logger = LogManager.GetCurrentClassLogger();
                logger.Debug($"Downloaded: {rangeDownloader.TotalBytesDownloaded}bytes in {rangeDownloader.RangesDownloaded} requests.");

                return rangeDownloader.TotalBytesDownloaded;
            }
            else
            {
                fileUri.ToString().DownloadFileAsync(output.FullName, cf.GetHeader().Filename).Wait();
                return cf.GetHeader().Length;

            }
        }

        public static void Sync(ControlFile controlFile, Stream seed, IRangeDownloader remoteFile, Stream output)
        {
            OutputFile.Patch(seed, controlFile, remoteFile, output);
        }
    }
}
