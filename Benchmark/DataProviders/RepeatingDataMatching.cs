namespace Benchmark.DataProviders;

internal class RepeatingDataMatching : ITestDataBuilder
{
    public (byte[] seed, byte[] original) Build(long size, int randomSeed)
    {
        var seed = new byte[size];
        Array.Fill(seed, (byte)(randomSeed / 255));
        var original = new byte[size];
        Array.Fill(original, (byte)(randomSeed / 255));
        return (seed, original);
    }
}
