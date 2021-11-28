using System;

namespace Tests
{
    internal class SynchronousProgress<T> : IProgress<T>
    {
        public void Report(T value)
        {
            ProgressChanged?.Invoke(value);
        }

        public event Action<T>? ProgressChanged;
    }
}
