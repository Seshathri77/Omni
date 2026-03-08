namespace OmniFlow.Sagas.Outbox;

/// <summary>
/// Interface for storing outbox messages for transactional messaging.
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Saves a message to the outbox (same transaction as saga state).
    /// </summary>
    Task SaveAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all unpublished messages.
    /// </summary>
    Task<IEnumerable<OutboxMessage>> GetUnpublishedAsync(int batchSize = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as published.
    /// </summary>
    Task MarkAsPublishedAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old published messages.
    /// </summary>
    Task DeleteOldMessagesAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a message in the outbox.
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The saga ID that created this message.
    /// </summary>
    public string SagaId { get; set; } = string.Empty;

    /// <summary>
    /// Message type name.
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Assembly-qualified type name for deserialization.
    /// </summary>
    public string MessageTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Serialized message envelope.
    /// </summary>
    public string MessageJson { get; set; } = string.Empty;

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the message was published (null if not yet published).
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// Number of publish attempts.
    /// </summary>
    public int PublishAttempts { get; set; }

    /// <summary>
    /// Last error message if publish failed.
    /// </summary>
    public string? LastError { get; set; }
}
