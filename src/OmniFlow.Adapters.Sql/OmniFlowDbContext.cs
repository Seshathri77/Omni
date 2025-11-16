using Microsoft.EntityFrameworkCore;
using OmniFlow.Idempotency;
using OmniFlow.Sagas;
using System.Text.Json;

namespace OmniFlow.Adapters.Sql;

/// <summary>
/// Entity Framework DbContext for OmniFlow persistence.
/// </summary>
public class OmniFlowDbContext : DbContext
{
    public OmniFlowDbContext(DbContextOptions<OmniFlowDbContext> options) : base(options)
    {
    }

    public DbSet<SagaStateEntity> SagaStates => Set<SagaStateEntity>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

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
