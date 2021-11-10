using System.IO;

namespace zsyncnet.Sync
{
    internal record SyncOperation(int BlockIndex, int BlockCount, bool IsLocal, long LocalOffset = 0, Stream Source = null);
}
