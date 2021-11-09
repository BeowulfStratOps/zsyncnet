using System;
using System.Globalization;
using NUnit.Framework;
using zsyncnet;

namespace Tests
{
    public class Md4Test
    {

        [Test]
        public void TestCorrectness1()
        {
            var data = new byte[2048];
            new Random(123).NextBytes(data);

            var hashBytes = new byte[16];
            new Md4(data.Length).Hash(data, 0, hashBytes);
            var hash = ZsyncUtil.ByteToHex(hashBytes);

            Assert.AreEqual("e44ba21ef5d141f3d5c97d34c9ac0542", hash);
        }

        [Test]
        public void TestCorrectness2()
        {
            var data = new byte[2048];
            new Random(456).NextBytes(data);

            var hashBytes = new byte[16];
            new Md4(data.Length).Hash(data, 0, hashBytes);
            var hash = ZsyncUtil.ByteToHex(hashBytes);

            Assert.AreEqual("2651e83ccad13c1054a0942844deb4e4", hash);
        }

        [Test]
        public void TestCorrectness3()
        {
            var data = new byte[2048];
            new Random(789).NextBytes(data);

            var hashBytes = new byte[16];
            new Md4(data.Length).Hash(data, 0, hashBytes);
            var hash = ZsyncUtil.ByteToHex(hashBytes);

            Assert.AreEqual("812481225a4cd3b722556ec916aae9cd", hash);
        }
    }
}
