using System;
using System.IO;

namespace zsyncnet
{
    public interface IRangeDownloader
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="from">Start (inclusive)</param>
        /// <param name="to">End (exclusive)</param>
        /// <returns></returns>
        Stream DownloadRange(long from, long to);

        Stream Download();
    }
}
