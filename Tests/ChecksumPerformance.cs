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
        private string tempPath;

        [SetUp]
        public void Setup()
        {
            tempPath = Path.GetTempFileName();
            var testData = new byte[100 * 1024 * 1024];
            File.WriteAllBytes(tempPath, testData);
        }

        [TearDown]
        public void TearDown()
        {
            File.Delete(tempPath);
        }

        [Test]
        public void RSum()
        {
            var start = DateTime.Now;

            var data = File.ReadAllBytes(tempPath);
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
            var start = DateTime.Now;

            var data = File.ReadAllBytes(tempPath);

            var md4Buffer = new byte[2048];

            for (int i = 0; i < 100_000; i++)
            {
                Array.Copy(data, i, md4Buffer, 0, 2048);
                ZsyncUtil.Md4Hash(md4Buffer);
            }

            var duration = (DateTime.Now - start);
            Assert.LessOrEqual(DateTime.Now - start, TimeSpan.FromSeconds(1));
            Console.WriteLine(duration.TotalSeconds.ToString(CultureInfo.InvariantCulture));
        }
    }
}
