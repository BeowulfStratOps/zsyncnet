namespace Benchmark.DataProviders;

public class RandomDataMatching : ITestDataBuilder
{
    public (byte[] seed, byte[] original) Build(long size, int randomSeed)
    {
        var random = new Random(randomSeed);

        var original = new byte[size];
        random.NextBytes(original);

        var seed = new byte[original.Length];
        original.CopyTo(seed, 0);

        return (seed, original);
    }
}
