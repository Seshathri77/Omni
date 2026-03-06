using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace OmniFlow.Adapters.Sql;

/// <summary>
/// SQL-based dead-letter queue store implementation.
/// </summary>
public class SqlDeadLetterQueueStore : Messaging.IDeadLetterQueueStore
{
    private readonly OmniFlowDbContext _context;

    public SqlDeadLetterQueueStore(OmniFlowDbContext context)
    {
        _context = context;
    }

    public async Task StoreAsync(Messaging.DeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        var entity = new DeadLetterQueueEntity
        {
            DeadLetterMessageId = message.DeadLetterMessageId,
            MessageId = message.MessageId,
            CorrelationId = message.CorrelationId,
            MessageBody = message.MessageBody,
            OriginalQueue = message.Metadata.OriginalQueue,
            OriginalMessageType = message.Metadata.OriginalMessageType,
            RetryCount = message.Metadata.RetryCount,
            FailureReasons = JsonSerializer.Serialize(message.Metadata.FailureReasons),
            FirstFailedAt = message.Metadata.FirstFailedAt,
            LastFailedAt = message.Metadata.LastFailedAt,
            NextRetryAt = message.Metadata.NextRetryAt,
            CreatedAt = message.CreatedAt
        };

        _context.DeadLetterQueue.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<Messaging.DeadLetterMessage>> GetMessagesAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.DeadLetterQueue
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDeadLetterMessage);
    }

    public async Task<Messaging.DeadLetterMessage?> GetAsync(
        string deadLetterMessageId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.DeadLetterQueue.FindAsync(
            new object[] { deadLetterMessageId },
            cancellationToken);

        return entity == null ? null : MapToDeadLetterMessage(entity);
    }

    public async Task RemoveAsync(string deadLetterMessageId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.DeadLetterQueue.FindAsync(
            new object[] { deadLetterMessageId },
            cancellationToken);

        if (entity != null)
        {
            _context.DeadLetterQueue.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateRetryMetadataAsync(
        string deadLetterMessageId,
        Messaging.DeadLetterMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.DeadLetterQueue.FindAsync(
            new object[] { deadLetterMessageId },
            cancellationToken);

        if (entity != null)
        {
            entity.RetryCount = metadata.RetryCount;
            entity.FailureReasons = JsonSerializer.Serialize(metadata.FailureReasons);
            entity.LastFailedAt = metadata.LastFailedAt;
            entity.NextRetryAt = metadata.NextRetryAt;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<Messaging.DeadLetterMessage>> GetRetryableMessagesAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entities = await _context.DeadLetterQueue
            .Where(e => e.NextRetryAt == null || e.NextRetryAt <= now)
            .OrderBy(e => e.FirstFailedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDeadLetterMessage);
    }

    private static Messaging.DeadLetterMessage MapToDeadLetterMessage(DeadLetterQueueEntity entity)
    {
        var failureReasons = JsonSerializer.Deserialize<string[]>(entity.FailureReasons) ?? Array.Empty<string>();

        return new Messaging.DeadLetterMessage
        {
            DeadLetterMessageId = entity.DeadLetterMessageId,
            MessageId = entity.MessageId,
            CorrelationId = entity.CorrelationId,
            MessageBody = entity.MessageBody,
            Metadata = new Messaging.DeadLetterMetadata
            {
                OriginalQueue = entity.OriginalQueue,
                OriginalMessageType = entity.OriginalMessageType,
                RetryCount = entity.RetryCount,
                FailureReasons = failureReasons,
                FirstFailedAt = entity.FirstFailedAt,
                LastFailedAt = entity.LastFailedAt,
                NextRetryAt = entity.NextRetryAt
            },
            CreatedAt = entity.CreatedAt
        };
    }
}
