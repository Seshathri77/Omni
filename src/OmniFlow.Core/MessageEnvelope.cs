namespace OmniFlow.Core;

/// <summary>
/// Envelope wrapping a message with metadata for correlation, tracing, and routing.
/// </summary>
/// <typeparam name="T">The type of the message payload.</typeparam>
public sealed class MessageEnvelope<T> where T : class
{
    /// <summary>
    /// Unique identifier for this message.
    /// </summary>
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Correlation ID for tracking related messages across service boundaries.
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Causation ID - the message ID that caused this message to be created.
    /// </summary>
    public string? CausationId { get; init; }

    /// <summary>
    /// Timestamp when the message was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The actual message payload.
    /// </summary>
    public required T Message { get; init; }

    /// <summary>
    /// Message type name for routing and deserialization.
    /// </summary>
    public string MessageType { get; init; } = typeof(T).Name;

    /// <summary>
    /// Schema version for message evolution and upcasting.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Optional metadata for extensibility.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Optional digital signature for message validation.
    /// </summary>
    public string? Signature { get; init; }

    /// <summary>
    /// Creates a new envelope from the current correlation context.
    /// </summary>
    public static MessageEnvelope<T> Create(T message, ICorrelationAccessor correlationAccessor)
    {
        return new MessageEnvelope<T>
        {
            Message = message,
            CorrelationId = correlationAccessor.CorrelationId ?? Guid.NewGuid().ToString(),
            CausationId = correlationAccessor.CorrelationId
        };
    }
}
