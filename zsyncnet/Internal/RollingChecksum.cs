using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// TODO: where should this be placed?
[assembly: InternalsVisibleTo("Tests")]

namespace zsyncnet.Internal
{
    internal static class RollingChecksum
    {
        public static IEnumerable<uint> GetRollingChecksum(byte[] array, int blockSize, int checksumBytes)
        {
            if (checksumBytes < 2 || checksumBytes > 4) throw new ArgumentException(null, nameof(checksumBytes));

            ushort a = 0, b = 0;
            for (int i = 0; i < blockSize; i++)
            {
                a += array[i];
                b += (ushort)((blockSize - i) * array[i]);
            }

            yield return ZsyncUtil.ToInt(a, b, checksumBytes);

            for (int i = 0; i < array.Length - blockSize; i++)
            {
                a = (ushort)(a - array[i] + array[i + blockSize]);
                b = (ushort)(b - blockSize * array[i] + a);
                yield return ZsyncUtil.ToInt(a, b, checksumBytes);
            }
        }
    }
}
