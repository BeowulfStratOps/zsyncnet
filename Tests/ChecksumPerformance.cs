using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using zsyncnet;
using zsyncnet.Internal;

namespace Tests
{
    public class ChecksumPerformance
    {
        [Test]
        public void RSum()
        {
            var data = new byte[100 * 1024 * 1024];
            new Random(123).NextBytes(data);

            var start = DateTime.Now;

            var rollingChecksum = RollingChecksum.GetRollingChecksum(data, 2048, 3);
            foreach (var _ in rollingChecksum)
            {
            }

            var duration = (DateTime.Now - start);
            Assert.LessOrEqual(DateTime.Now - start, TimeSpan.FromSeconds(1));
            Console.WriteLine(duration.TotalSeconds.ToString(CultureInfo.InvariantCulture));
        }

        [Test]
        public void Md4()
        {
            var data = new byte[100 * 1024 * 1024];
            new Random(123).NextBytes(data);
            var md4Hasher = new Md4(2048);

            var start = DateTime.Now;

            for (int i = 0; i < 100_000; i++)
            {
                md4Hasher.Hash(data, i);
            }

            var duration = (DateTime.Now - start);
            Assert.LessOrEqual(DateTime.Now - start, TimeSpan.FromSeconds(1));
            Console.WriteLine(duration.TotalSeconds.ToString(CultureInfo.InvariantCulture));
        }
    }
}
