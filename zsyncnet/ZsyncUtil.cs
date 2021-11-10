using System;
using System.Linq;
using System.Text;

namespace zsyncnet
{
    internal static class ZsyncUtil
    {
        public static uint ComputeRsum(Span<byte> block, int checkSumBytes)
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
    }
}
