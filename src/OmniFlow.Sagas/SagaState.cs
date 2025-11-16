namespace OmniFlow.Sagas;

/// <summary>
/// Base class for saga state.
/// </summary>
public abstract class SagaState
{
    /// <summary>
    /// Unique identifier for the saga instance.
    /// </summary>
    public string SagaId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the saga.
    /// </summary>
    public SagaStatus Status { get; set; } = SagaStatus.Running;

    /// <summary>
    /// Version for optimistic concurrency control.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// When the saga was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the saga was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// History of state transitions for debugging.
    /// </summary>
    public List<string> History { get; set; } = new();
}

/// <summary>
/// Possible saga statuses.
/// </summary>
public enum SagaStatus
{
    Running,
    Completed,
    Compensating,
    Compensated,
    Failed
}
