using Microsoft.EntityFrameworkCore;
using OmniFlow.Idempotency;
using OmniFlow.Sagas;
using OmniFlow.Messaging;
using System.Text.Json;

namespace OmniFlow.Adapters.Sql;

/// <summary>
/// Entity Framework DbContext for OmniFlow persistence.
/// </summary>
public class OmniFlowDbContext : DbContext, ISagaDbContext
{
    public OmniFlowDbContext(DbContextOptions<OmniFlowDbContext> options) : base(options)
    {
    }

    public DbSet<SagaStateEntity> SagaStates => Set<SagaStateEntity>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<SagaTimer> SagaTimers => Set<SagaTimer>();
    public DbSet<DistributedLockEntity> DistributedLocks => Set<DistributedLockEntity>();
    public DbSet<DeadLetterQueueEntity> DeadLetterQueue => Set<DeadLetterQueueEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SagaStateEntity>(entity =>
        {
            entity.HasKey(e => e.SagaId);
            entity.HasIndex(e => e.CorrelationId);
            entity.Property(e => e.StateJson).HasMaxLength(8000);
            entity.Property(e => e.SagaType).HasMaxLength(500);
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasKey(e => new { e.MessageId, e.ConsumerName });
            entity.HasIndex(e => e.ProcessedAt);
        });

        modelBuilder.Entity<SagaTimer>(entity =>
        {
            entity.HasKey(e => e.TimerId);
            entity.HasIndex(e => e.SagaId);
            entity.HasIndex(e => new { e.IsCompleted, e.FireAt });
        });

        modelBuilder.Entity<DistributedLockEntity>(entity =>
        {
            entity.HasKey(e => e.LockKey);
            entity.HasIndex(e => e.ExpiresAt);
        });

        modelBuilder.Entity<DeadLetterQueueEntity>(entity =>
        {
            entity.HasKey(e => e.DeadLetterMessageId);
            entity.HasIndex(e => e.CorrelationId);
            entity.HasIndex(e => new { e.RetryCount, e.NextRetryAt });
            entity.Property(e => e.MessageBody).HasMaxLength(8000);
            entity.Property(e => e.FailureReasons).HasMaxLength(4000);
        });
    }
}

/// <summary>
/// Entity for storing saga state.
/// </summary>
public class SagaStateEntity
{
    public string SagaId { get; set; } = string.Empty;
    public string SagaType { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string StateJson { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Entity for storing idempotency records.
/// </summary>
public class IdempotencyRecord
{
    public string MessageId { get; set; } = string.Empty;
    public string ConsumerName { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary>
/// Entity for storing dead-letter queue messages.
/// </summary>
public class DeadLetterQueueEntity
{
    public string DeadLetterMessageId { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public string OriginalQueue { get; set; } = string.Empty;
    public string OriginalMessageType { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string FailureReasons { get; set; } = string.Empty; // JSON array
    public DateTimeOffset FirstFailedAt { get; set; }
    public DateTimeOffset LastFailedAt { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
