namespace Benchmark.DataProviders;

public abstract class RandomDataRandomCopies
{
    protected (byte[] seed, byte[] original) Build(long originalSize, long seedSize, int randomSeed)
    {
        var random = new Random(randomSeed);

        var original = new byte[originalSize];
        random.NextBytes(original);

        var seed = new byte[seedSize];
        random.NextBytes(original);

        for (int i = 0; i < seedSize / 1024 / 100; i++) // 10 copies per mb. because why not.
        {
            var source = random.NextInt64(originalSize);
            var destination = random.NextInt64(seedSize);
            var copySize = random.NextInt64(seedSize / 3);

            if (originalSize - source < copySize)
                copySize = originalSize - source;
            if (seedSize - destination < copySize)
                copySize = seedSize - destination;

            Array.Copy(original, source, seed, destination, copySize);
        }

        return (seed, original);
    }
}

internal class RandomDataSameSizeRandomCopies : RandomDataRandomCopies, ITestDataBuilder
{
    public (byte[] seed, byte[] original) Build(long size, int randomSeed)
    {
        return Build(size, size, randomSeed);
    }
}

internal class RandomDataSmallerRandomCopies : RandomDataRandomCopies, ITestDataBuilder
{
    public (byte[] seed, byte[] original) Build(long size, int randomSeed)
    {
        return Build(size, size / 2, randomSeed);
    }
}

internal class RandomDataBiggerRandomCopies : RandomDataRandomCopies, ITestDataBuilder
{
    public (byte[] seed, byte[] original) Build(long size, int randomSeed)
    {
        return Build(size, size * 2, randomSeed);
    }
}
