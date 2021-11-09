using System;
using System.Linq;

namespace zsyncnet
{
    public class Md4
    {
        private readonly int _length;

        public Md4(int length)
        {
            if (length % 4 != 0) throw new ArgumentException("length must be multiple of 4", nameof(length));
            _length = length;

            // make it so that (length + 1 + padBytes) % 64 == 56
            var size = length + 1;
            var padBytes = (64 + 56 - size % 64) % 64;
            size += padBytes;

            _uints = new uint[size / 4 + 2];
        }

        private readonly uint[] _uints;

        private static readonly uint[] Y1 = { 0, 4, 8, 12, 0, 1, 2, 3, 3, 7, 11, 19, 0 };
        private static readonly uint[] Y2 = { 0, 1, 2, 3, 0, 4, 8, 12, 3, 5, 9, 13, 0x5a827999 };
        private static readonly uint[] Y3 = { 0, 2, 1, 3, 0, 8, 4, 12, 3, 9, 11, 15, 0x6ed9eba1 };

        public void Hash(byte[] input, long offset, byte[] output)
        {
            if (output.Length != 16) throw new ArgumentException("output buffer must be 16bytes long", nameof(output));

            for (int i = 0; i + 3 < _length; i += 4)
                _uints[i / 4] = input[i + offset] | (uint)input[i + offset + 1] << 8 | (uint)input[i + offset + 2] << 16 | (uint)input[i + offset + 3] << 24;
            _uints[_length / 4] = 128;
            var fillStart = _length / 4 + 1;
            Array.Fill<uint>(_uints, 0, fillStart, _uints.Length - fillStart);
            _uints[^2] = (uint) _length * 8; // bitcount
            _uints[^1] = 0;

            // run rounds
            uint a = 0x67452301, b = 0xefcdab89, c = 0x98badcfe, d = 0x10325476;
            Func<uint, uint, uint> rol = (x, y) => x << (int) y | x >> 32 - (int) y;
            for (int q = 0; q + 15 < _uints.Length; q += 16)
            {
                uint aa = a, bb = b, cc = c, dd = d;

                void Round(Func<uint, uint, uint, uint> f, uint[] y)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        var i = y[j];
                        a = rol(a + f(b, c, d) + _uints[q + (int)(i + y[4])] + y[12], y[8]);
                        d = rol(d + f(a, b, c) + _uints[q + (int)(i + y[5])] + y[12], y[9]);
                        c = rol(c + f(d, a, b) + _uints[q + (int)(i + y[6])] + y[12], y[10]);
                        b = rol(b + f(c, d, a) + _uints[q + (int)(i + y[7])] + y[12], y[11]);
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

            WriteUIntToByteArray(output, 0, a);
            WriteUIntToByteArray(output, 4, b);
            WriteUIntToByteArray(output, 8, c);
            WriteUIntToByteArray(output, 12, d);
        }

        private static void WriteUIntToByteArray(byte[] array, int offset, uint number)
        {
            array[offset] = (byte)(number & 0xff);
            number >>= 8;
            array[offset + 1] = (byte)(number & 0xff);
            number >>= 8;
            array[offset + 2] = (byte)(number & 0xff);
            number >>= 8;
            array[offset + 3] = (byte)(number & 0xff);
        }
    }
}
