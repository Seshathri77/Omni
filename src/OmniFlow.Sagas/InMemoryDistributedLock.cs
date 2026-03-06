using System.Collections.Concurrent;

namespace OmniFlow.Sagas;

/// <summary>
/// In-memory distributed lock implementation for development and testing.
/// </summary>
public class InMemoryDistributedLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, LockEntry> _locks = new();

    public Task<IAsyncDisposable?> AcquireAsync(
        string key,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(timeout);
        var lockEntry = new LockEntry(expiresAt);

        if (_locks.TryAdd(key, lockEntry))
        {
            return Task.FromResult<IAsyncDisposable?>(new LockHandle(key, this));
        }

        // Check if existing lock is expired
        if (_locks.TryGetValue(key, out var existingLock))
        {
            if (existingLock.ExpiresAt < DateTimeOffset.UtcNow)
            {
                // Expired, try to replace
                if (_locks.TryUpdate(key, lockEntry, existingLock))
                {
                    return Task.FromResult<IAsyncDisposable?>(new LockHandle(key, this));
                }
            }
        }

        // Lock held by another process
        return Task.FromResult<IAsyncDisposable?>(null);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_locks.TryGetValue(key, out var lockEntry))
        {
            // Check if not expired
            return Task.FromResult(lockEntry.ExpiresAt > DateTimeOffset.UtcNow);
        }
        return Task.FromResult(false);
    }

    public Task ReleaseAsync(string key, CancellationToken cancellationToken = default)
    {
        _locks.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private record LockEntry(DateTimeOffset ExpiresAt);

    private class LockHandle : IAsyncDisposable
    {
        private readonly string _key;
        private readonly InMemoryDistributedLock _lock;

        public LockHandle(string key, InMemoryDistributedLock distributedLock)
        {
            _key = key;
            _lock = distributedLock;
        }

        public async ValueTask DisposeAsync()
        {
            await _lock.ReleaseAsync(_key);
        }
    }
}
