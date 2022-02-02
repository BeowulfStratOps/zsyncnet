using Benchmark;
using Benchmark.DataProviders;
using zsyncnet;

const int timeoutSeconds = 30;
const int mb = 100;

var seeds = new[] { 1, 2, 4, 69, 420, 1337, 12345 };

foreach (var builder in new ITestDataBuilder[]
         {
             // TODO: check real world data!!
             new RandomDataMatching(),
             new RandomDataSameSizeRandomCopies(),
             new RandomDataSmallerRandomCopies(),
             new RandomDataBiggerRandomCopies(),
             new RepeatingDataMatching()
         })
{
    var name = builder.GetType().Name;

    var totalTime = TimeSpan.Zero;

    foreach (var randomSeed in seeds)
    {
        var size = (long)mb * 1024 * 1024;
        var (seed, original) = builder.Build(size, randomSeed);
        using var originalStream = new MemoryStream(original);
        var cf = ZsyncMake.MakeControlFile(originalStream, DateTime.Now, "");

        var output = new MemoryStream();
        var seedStream = new MemoryStream(seed);

        var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        var start = DateTime.Now;

        try
        {
            Zsync.Sync(cf, new List<Stream> { seedStream }, new MockDownloader(original), output,
                cancellationToken: timeout.Token);
            totalTime += DateTime.Now - start;
        }
        catch (OperationCanceledException)
        {
            var time = DateTime.Now - start;
            Console.WriteLine(
                $"Syncing {mb}mb from {name} timed out after {time.TotalSeconds:F2}s (timeout was {timeoutSeconds}s)");
        }
    }

    var avgTime = (totalTime.TotalSeconds) / seeds.Length;
    Console.WriteLine($"Average {mb}mb from {name}: {avgTime:F2}s");
}
