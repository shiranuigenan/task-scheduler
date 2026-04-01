namespace TaskScheduler.Application.Abstractions;

/// <summary>
/// In-memory group locks: same group key cannot run concurrently; lock until execution (including retries) completes.
/// </summary>
public interface IGroupLockManager
{
    /// <summary>Acquires the lock if the group is free; otherwise returns null.</summary>
    ValueTask<IAsyncDisposable?> TryAcquireAsync(string groupKey, CancellationToken cancellationToken = default);
}
