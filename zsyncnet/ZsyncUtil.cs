using System;
using System.Linq;
using System.Text;

namespace zsyncnet
{
    public static class ZsyncUtil
    {
        public static uint ComputeRsum(byte[] block, int checkSumBytes)
        {
            ushort a = 0;
            ushort b = 0;
            for (int i = 0, l = block.Length; i < block.Length; i++, l--)
            {
                a += block[i];
                b += (ushort)(l * block[i]);
            }

            return ToInt(a, b, checkSumBytes);
        }

        private static readonly uint[] BitMasks2To4 = { 0xffff, 0xffffff, 0xffffffff };

        public static uint ToInt(ushort x, ushort y, int bytes)
        {
            if (bytes < 2 || bytes > 4) throw new ArgumentException(null, nameof(bytes));
            var result = (uint)(x << 16) | y;
            return result & BitMasks2To4[bytes - 2];
        }

        public static string ByteToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();

        }

        private static readonly uint[] Y1 = { 0, 4, 8, 12, 0, 1, 2, 3, 3, 7, 11, 19, 0 };
        private static readonly uint[] Y2 = { 0, 1, 2, 3, 0, 4, 8, 12, 3, 5, 9, 13, 0x5a827999 };
        private static readonly uint[] Y3 = { 0, 2, 1, 3, 0, 8, 4, 12, 3, 9, 11, 15, 0x6ed9eba1 };

        public static byte[] Md4Hash(byte[] input)
        {
            var size = input.Length + 1;
            while (size % 64 != 56) size++; // TODO: go back to school
            var bytes = new byte[size];
            Array.Fill<byte>(bytes, 0);
            input.CopyTo(bytes, 0);
            bytes[input.Length] = 128;

            uint bitCount = (uint) (input.Length) * 8;
            var uints = new uint[bytes.Length / 4 + 2];
            for (int i = 0; i + 3 < bytes.Length; i += 4)
                uints[i / 4] = bytes[i] | (uint)bytes[i + 1] << 8 | (uint)bytes[i + 2] << 16 | (uint)bytes[i + 3] << 24;
            uints[^2] = bitCount;
            uints[^1] = 0;

            // run rounds
            uint a = 0x67452301, b = 0xefcdab89, c = 0x98badcfe, d = 0x10325476;
            Func<uint, uint, uint> rol = (x, y) => x << (int) y | x >> 32 - (int) y;
            for (int q = 0; q + 15 < uints.Length; q += 16)
            {
                uint aa = a, bb = b, cc = c, dd = d;

                void Round(Func<uint, uint, uint, uint> f, uint[] y)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        var i = y[j];
                        a = rol(a + f(b, c, d) + uints[q + (int)(i + y[4])] + y[12], y[8]);
                        d = rol(d + f(a, b, c) + uints[q + (int)(i + y[5])] + y[12], y[9]);
                        c = rol(c + f(d, a, b) + uints[q + (int)(i + y[6])] + y[12], y[10]);
                        b = rol(b + f(c, d, a) + uints[q + (int)(i + y[7])] + y[12], y[11]);
                    }
                }

                Round((x, y, z) => (x & y) | (~x & z), Y1);
                Round((x, y, z) => (x & y) | (x & z) | (y & z), Y2);
                Round((x, y, z) => x ^ y ^ z, Y3);
                a += aa;
                b += bb;
                c += cc;
                d += dd;
            }

            // return hex encoded string
            byte[] outBytes = new[] {a, b, c, d}.SelectMany(BitConverter.GetBytes).ToArray();
            return outBytes;
        }
    }
}
