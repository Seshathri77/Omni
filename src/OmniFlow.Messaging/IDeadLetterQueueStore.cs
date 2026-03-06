namespace OmniFlow.Messaging;

/// <summary>
/// Metadata for dead-letter queue messages.
/// </summary>
public record DeadLetterMetadata
{
    public string OriginalQueue { get; init; } = string.Empty;
    public string OriginalMessageType { get; init; } = string.Empty;
    public int RetryCount { get; init; }
    public string[] FailureReasons { get; init; } = Array.Empty<string>();
    public DateTimeOffset FirstFailedAt { get; init; }
    public DateTimeOffset LastFailedAt { get; init; }
    public DateTimeOffset? NextRetryAt { get; init; }
}

/// <summary>
/// Dead-letter queue message wrapper.
/// </summary>
public record DeadLetterMessage
{
    public string DeadLetterMessageId { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string MessageBody { get; init; } = string.Empty;
    public DeadLetterMetadata Metadata { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Storage for dead-letter queue messages.
/// </summary>
public interface IDeadLetterQueueStore
{
    /// <summary>
    /// Stores a message in the dead-letter queue.
    /// </summary>
    Task StoreAsync(DeadLetterMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets messages from the dead-letter queue.
    /// </summary>
    Task<IEnumerable<DeadLetterMessage>> GetMessagesAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific dead-letter message.
    /// </summary>
    Task<DeadLetterMessage?> GetAsync(string deadLetterMessageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a message from the dead-letter queue.
    /// </summary>
    Task RemoveAsync(string deadLetterMessageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates retry metadata for a message.
    /// </summary>
    Task UpdateRetryMetadataAsync(
        string deadLetterMessageId,
        DeadLetterMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets messages ready for retry.
    /// </summary>
    Task<IEnumerable<DeadLetterMessage>> GetRetryableMessagesAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default);
}
