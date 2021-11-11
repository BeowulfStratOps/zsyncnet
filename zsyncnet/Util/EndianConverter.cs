using System;

namespace zsyncnet.Util
{
    public static class EndianConverter
    {
        public static byte[] ToBigEndian(uint value, int byteCount)
        {
            if (byteCount is < 2 or > 4) throw new ArgumentException(null, nameof(byteCount));

            var result = new byte[byteCount];

            for (var i = byteCount - 1; i >= 0; i--)
            {
                result[i] = (byte)(value & 0xff);
                value >>= 8;
            }

            return result;
        }

        public static uint FromBigEndian(byte[] block)
        {
            uint result = 0;
            foreach (var t in block)
            {
                result <<= 8;
                result += t;
            }
            return result;
        }
    }
}
