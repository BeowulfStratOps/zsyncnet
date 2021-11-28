using System.IO;

namespace zsyncnet.Sync
{
    internal record DownloadRange(int BlockIndex, int BlockCount);
}
