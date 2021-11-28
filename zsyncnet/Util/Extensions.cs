using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace zsyncnet.Util
{
    internal static class ArrayExtensions
    {
        public static int [] Locate (this byte [] self, byte [] candidate)
        {
            if (IsEmptyLocate (self, candidate))
                return Array.Empty<int>();

            var list = new List<int> ();

            for (int i = 0; i < self.Length; i++) {
                if (!IsMatch (self, i, candidate))
                    continue;

                list.Add (i);
            }

            return list.Count == 0 ? Array.Empty<int>() : list.ToArray ();
        }

        private static bool IsMatch (byte [] array, int position, byte [] candidate)
        {
            if (candidate.Length > (array.Length - position))
                return false;

            for (int i = 0; i < candidate.Length; i++)
                if (array [position + i] != candidate [i])
                    return false;

            return true;
        }

        private static bool IsEmptyLocate (byte [] array, byte [] candidate)
        {
            return array == null
                   || candidate == null
                   || array.Length == 0
                   || candidate.Length == 0
                   || candidate.Length > array.Length;
        }

        public static byte[] ToByteArray(this Stream stream)
        {
            using(var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        public static string ToHex(this byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);

            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        public static void CopyToWithProgress(this Stream source, Stream destination, int bufferSize, IProgress<ulong> progress)
        {
            // borrowed from Stream.CopyTo
            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) != 0)
                {
                    destination.Write(buffer, 0, read);
                    progress?.Report((ulong)read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
