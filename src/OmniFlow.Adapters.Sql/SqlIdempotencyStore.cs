using Microsoft.EntityFrameworkCore;
using OmniFlow.Idempotency;

namespace OmniFlow.Adapters.Sql;

/// <summary>
/// SQL-based idempotency store using Entity Framework.
/// </summary>
public class SqlIdempotencyStore : IIdempotencyStore
{
    private readonly OmniFlowDbContext _context;

    public SqlIdempotencyStore(OmniFlowDbContext context)
    {
        _context = context;
    }

    public async Task<bool> TryRecordAsync(
        string messageId,
        string consumerName,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        var exists = await ExistsAsync(messageId, consumerName, cancellationToken);
        if (exists)
            return false;

        var record = new IdempotencyRecord
        {
            MessageId = messageId,
            ConsumerName = consumerName,
            ProcessedAt = DateTimeOffset.UtcNow,
            ExpiresAt = ttl.HasValue ? DateTimeOffset.UtcNow.Add(ttl.Value) : null
        };

        _context.IdempotencyRecords.Add(record);
        
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Duplicate key - message already processed
            return false;
        }
    }

    public async Task<bool> ExistsAsync(
        string messageId,
        string consumerName,
        CancellationToken cancellationToken = default)
    {
        return await _context.IdempotencyRecords
            .AnyAsync(r => r.MessageId == messageId && r.ConsumerName == consumerName, cancellationToken);
    }

    public async Task RemoveAsync(
        string messageId,
        string consumerName,
        CancellationToken cancellationToken = default)
    {
        var record = await _context.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.MessageId == messageId && r.ConsumerName == consumerName, cancellationToken);

        if (record != null)
        {
            _context.IdempotencyRecords.Remove(record);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
