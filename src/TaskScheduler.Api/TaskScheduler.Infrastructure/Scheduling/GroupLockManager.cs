using System.Collections.Concurrent;
using TaskScheduler.Application.Abstractions;

namespace TaskScheduler.Infrastructure.Scheduling;

public sealed class GroupLockManager : IGroupLockManager
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<IAsyncDisposable?> TryAcquireAsync(string groupKey, CancellationToken cancellationToken = default)
    {
        var sem = _locks.GetOrAdd(groupKey, _ => new SemaphoreSlim(1, 1));
        if (!await sem.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return null;

        return new Releaser(sem);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
