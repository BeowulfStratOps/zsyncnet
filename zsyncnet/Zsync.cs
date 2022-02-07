using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using zsyncnet.Sync;
using zsyncnet.Util;

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
        /// Syncs a file in the output folder from a remote file. Can handle .part files.
        /// </summary>
        /// <param name="zsyncFile">Uri to the remote file. A .zsync file is assumed to be next to it and will be used to find or create the local file</param>
        /// <param name="output">Folder in which the work file and a potential .part file exist or will be created.</param>
        /// <param name="cancellationToken">Cancels the syncing operation. Downloaded data is continuously written to the workingStream and will not be lost.</param>
        /// <param name="progress">Receives incremental progress in bytes. The total sum will be equal to the target file size when the operation is complete.</param>
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
        /// Syncs a file in the output folder from a control file and file downloader. Can handle .part files.
        /// </summary>
        /// <param name="controlFile">The control file. The filename is used to find or create the working file in the output folder</param>
        /// <param name="downloader">Downloader for the remote file.</param>
        /// <param name="output">Folder in which the work file and a potential .part file exist or will be created.</param>
        /// <param name="cancellationToken">Cancels the syncing operation. Downloaded data is continuously written to the workingStream and will not be lost.</param>
        /// <param name="progress">Receives incremental progress in bytes. The total sum will be equal to the target file size when the operation is complete.</param>
        public static void Sync(ControlFile controlFile, IRangeDownloader downloader, DirectoryInfo output, IProgress<ulong> progress = null, CancellationToken cancellationToken = default)
        {
            var path = Path.Combine(output.FullName, controlFile.GetHeader().Filename.Trim());
            if (!File.Exists(path))
            {
                // File does not exist on disk, we just need to download it
                var downloadStream = downloader.Download();

                var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                downloadStream.CopyToWithProgress(stream, 2024, progress);

                File.SetLastWriteTime(path, controlFile.GetHeader().MTime);
                return;
            }

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
        /// Patches a file (workingStream) according to the passed controlFile. Additional seeds can be specified.
        /// </summary>
        /// <param name="controlFile">The control file. The filename in it is ignored.</param>
        /// <param name="seeds">Additional seeding streams. Must not include the workingStream. Streams are not closed.</param>
        /// <param name="downloader">Downloader for the remote file.</param>
        /// <param name="workingStream">Working Stream. If it contains any data, that data is used as a seed. Will not be closed.</param>
        /// <param name="cancellationToken">Cancels the syncing operation. Downloaded data is continuously written to the workingStream and will not be lost.</param>
        /// <param name="progress">Receives incremental progress in bytes. The total sum will be equal to the target file size when the operation is complete.</param>
        public static void Sync(ControlFile controlFile, List<Stream> seeds, IRangeDownloader downloader, Stream workingStream, IProgress<ulong> progress = null, CancellationToken cancellationToken = default)
        {
            ZsyncPatch.Patch(seeds, controlFile, downloader, workingStream, progress, cancellationToken);
        }
    }
}
