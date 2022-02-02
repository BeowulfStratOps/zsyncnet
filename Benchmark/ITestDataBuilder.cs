namespace Benchmark;

internal interface ITestDataBuilder
{
    (byte[] seed, byte[] original) Build(long size, int randomSeed);
}
