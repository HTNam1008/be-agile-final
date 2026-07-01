namespace Moe.Application.Abstractions.Persistence;

public interface IDistributedLock
{
    /// <summary>
    /// Attempts to acquire a distributed lock. Returns true if acquired, false if already locked by another process.
    /// </summary>
    /// <param name="lockKey">The unique key identifying the resource to lock.</param>
    /// <param name="timeout">How long to hold the lock before it automatically expires.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the lock was acquired, false otherwise.</returns>
    Task<bool> TryAcquireAsync(string lockKey, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the distributed lock.
    /// </summary>
    /// <param name="lockKey">The unique key identifying the resource.</param>
    Task ReleaseAsync(string lockKey);
}
