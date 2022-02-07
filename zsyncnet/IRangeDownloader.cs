using System;
using System.IO;

namespace zsyncnet
{
    public interface IRangeDownloader
    {
        /// <summary>
        /// Starts the download of a file section. Should return after receiving headers and opening the stream, so that the reading can be done asynchronously.
        /// </summary>
        /// <param name="from">Start (inclusive)</param>
        /// <param name="to">End (exclusive)</param>
        /// <returns></returns>
        Stream DownloadRange(long from, long to);

        /// <summary>
        /// Starts the download of the entire file. Should return after receiving headers and opening the stream, so that the reading can be done asynchronously.
        /// </summary>
        /// <returns></returns>
        Stream Download();
    }
}
