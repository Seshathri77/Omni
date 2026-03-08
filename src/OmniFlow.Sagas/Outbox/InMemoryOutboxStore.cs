using System.Collections.Concurrent;

namespace OmniFlow.Sagas.Outbox;

/// <summary>
/// In-memory implementation of outbox store for testing.
/// </summary>
public class InMemoryOutboxStore : IOutboxStore
{
    private readonly ConcurrentDictionary<string, OutboxMessage> _messages = new();

    public Task SaveAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _messages[message.MessageId] = message;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<OutboxMessage>> GetUnpublishedAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var unpublished = _messages.Values
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize);

        return Task.FromResult(unpublished);
    }

    public Task MarkAsPublishedAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var message))
        {
            message.PublishedAt = DateTimeOffset.UtcNow;
        }
        return Task.CompletedTask;
    }

    public Task DeleteOldMessagesAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - olderThan;
        var toDelete = _messages.Values
            .Where(m => m.PublishedAt.HasValue && m.PublishedAt < cutoff)
            .Select(m => m.MessageId)
            .ToList();

        foreach (var id in toDelete)
        {
            _messages.TryRemove(id, out _);
        }

        return Task.CompletedTask;
    }
}
