using Microsoft.EntityFrameworkCore;

namespace OmniFlow.Sagas;

/// <summary>
/// Database context interface for saga infrastructure persistence.
/// This interface is implemented by OmniFlow.Adapters.Sql.OmniFlowDbContext.
/// </summary>
public interface ISagaDbContext
{
    /// <summary>
    /// Saga timers for durable timeout handling.
    /// </summary>
    DbSet<SagaTimer> SagaTimers { get; }

    /// <summary>
    /// Distributed locks for saga coordination.
    /// </summary>
    DbSet<DistributedLockEntity> DistributedLocks { get; }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Saga timer entity for SQL persistence.
/// </summary>
public class SagaTimer
{
    public string TimerId { get; set; } = string.Empty;
    public string SagaId { get; set; } = string.Empty;
    public string TimerName { get; set; } = string.Empty;
    public DateTimeOffset FireAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset? FiredAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}

/// <summary>
/// Distributed lock entity for SQL persistence.
/// </summary>
public class DistributedLockEntity
{
    public string LockKey { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public DateTimeOffset AcquiredAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
