using System.Collections.Concurrent;

namespace TaskScheduler.Infrastructure.Scheduling;

public sealed class GroupLockManager
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public IDisposable? TryAcquire(string groupKey, CancellationToken cancellationToken = default)
    {
        var sem = _locks.GetOrAdd(groupKey, _ => new SemaphoreSlim(1, 1));

        if (!sem.Wait(0, cancellationToken))
            return null;

        return new Releaser(sem);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                semaphore.Release();
        }
    }
}
