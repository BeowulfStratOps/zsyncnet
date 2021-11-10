using System;

namespace zsyncnet.Hash
{
    internal class RollingChecksum
    {
        private readonly byte[] _array;
        private readonly int _blockSize;
        private ushort _a, _b;
        private long _index;
        private readonly uint _mask;

        private static readonly uint[] BitMasks2To4 = { 0xffff, 0xffffff, 0xffffffff };

        public RollingChecksum(byte[] array, int blockSize, int checksumBytes)
        {
            if (checksumBytes < 2 || checksumBytes > 4) throw new ArgumentException(null, nameof(checksumBytes));
            _mask = BitMasks2To4[checksumBytes - 2];

            _array = array;
            _blockSize = blockSize;

            for (_index = 0; _index < blockSize; _index++)
            {
                _a += array[_index];
                _b += (ushort)((blockSize - _index) * array[_index]);
            }
        }

        public uint Current => _mask & (uint)(_a << 16) | _b;

        public void Next()
        {
            _a = (ushort)(_a - _array[_index - _blockSize] + _array[_index]);
            _b = (ushort)(_b - _blockSize * _array[_index - _blockSize] + _a);
            _index++;
        }
    }
}
