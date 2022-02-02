using zsyncnet;

namespace Benchmark;

public class MockDownloader : IRangeDownloader
{
    private readonly byte[] _data;

    public MockDownloader(byte[] data)
    {
        _data = data;
    }

    public Stream DownloadRange(long @from, long to)
    {
        return new MemoryStream(_data, (int)from, (int)(to - @from), false);
    }

    public Stream Download()
    {
        throw new NotImplementedException();
    }
}
