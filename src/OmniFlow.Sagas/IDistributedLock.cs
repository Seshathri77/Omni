namespace OmniFlow.Sagas;

/// <summary>
/// Abstraction for distributed locking to prevent duplicate saga starts.
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// Attempts to acquire a distributed lock.
    /// </summary>
    /// <param name="key">The lock key (e.g., "saga:order:123").</param>
    /// <param name="timeout">How long the lock is valid.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Lock handle if acquired, null if lock is held by another instance.</returns>
    Task<IAsyncDisposable?> AcquireAsync(
        string key,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a lock exists.
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a lock by key.
    /// </summary>
    Task ReleaseAsync(string key, CancellationToken cancellationToken = default);
}
