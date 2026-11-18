using System;
using System.Threading;

namespace ReimaginedLauncher.Utilities;

internal static class LauncherOperationGate
{
    private static int _busy;
    internal static bool IsBusy => Volatile.Read(ref _busy) != 0;

    internal static IDisposable? TryAcquire()
        => Interlocked.CompareExchange(ref _busy, 1, 0) == 0 ? new Lease() : null;

    private sealed class Lease : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) Volatile.Write(ref _busy, 0);
        }
    }
}
