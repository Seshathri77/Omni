using Microsoft.Extensions.Caching.Memory;

namespace OmniFlow.Idempotency;

/// <summary>
/// In-memory idempotency store for testing and single-instance scenarios.
/// </summary>
public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly IMemoryCache _cache;

    public InMemoryIdempotencyStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc/>
    public Task<bool> TryRecordAsync(
        string messageId,
        string consumerName,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(messageId, consumerName);
        
        // Try to set if not exists
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromDays(7)
        };

        if (_cache.TryGetValue(key, out _))
        {
            return Task.FromResult(false); // Already exists
        }

        _cache.Set(key, DateTimeOffset.UtcNow, options);
        return Task.FromResult(true); // Newly recorded
    }

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(
        string messageId,
        string consumerName,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(messageId, consumerName);
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }

    /// <inheritdoc/>
    public Task RemoveAsync(
        string messageId,
        string consumerName,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(messageId, consumerName);
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    private static string GetKey(string messageId, string consumerName)
    {
        return $"idempotency:{consumerName}:{messageId}";
    }
}
