namespace OmniFlow.Core;

/// <summary>
/// Context for message processing containing correlation and metadata.
/// </summary>
public sealed class MessageContext
{
    /// <summary>
    /// Correlation ID for the current message.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Causation ID (message that triggered this one).
    /// </summary>
    public string? CausationId { get; init; }

    /// <summary>
    /// Message ID being processed.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Message type.
    /// </summary>
    public required string MessageType { get; init; }

    /// <summary>
    /// Timestamp when message was received.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Cancellation token for the message processing operation.
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = default;

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = 
        new Dictionary<string, string>();

    /// <summary>
    /// Creates a context from a message envelope.
    /// </summary>
    public static MessageContext FromEnvelope<T>(MessageEnvelope<T> envelope) where T : class
    {
        return FromEnvelope(envelope, CancellationToken.None);
    }

    /// <summary>
    /// Creates a context from a message envelope with a cancellation token.
    /// </summary>
    public static MessageContext FromEnvelope<T>(MessageEnvelope<T> envelope, CancellationToken cancellationToken) where T : class
    {
        return new MessageContext
        {
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Timestamp = envelope.Timestamp,
            Metadata = envelope.Metadata,
            CancellationToken = cancellationToken
        };
    }
}
