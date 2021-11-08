using System.Dynamic;

namespace zsyncnet.Internal
{
    internal record SyncOperation(int BlockIndex, int BlockCount, bool IsLocal, long LocalOffset = 0);
}
