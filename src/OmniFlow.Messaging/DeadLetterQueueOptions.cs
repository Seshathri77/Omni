using OmniFlow.Core;

namespace OmniFlow.Messaging;

/// <summary>
/// Configuration for dead letter queue handling.
/// This class is used internally by message bus adapters.
/// For unified configuration, use <see cref="DeadLetterQueueConfig"/> in <see cref="OmniFlowOptions"/>.
/// </summary>
public class DeadLetterQueueOptions
{
    /// <summary>
    /// Maximum number of retry attempts before sending to DLQ.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Dead letter queue name.
    /// </summary>
    public string QueueName { get; set; } = "dead-letter-queue";

    /// <summary>
    /// Dead letter exchange name.
    /// </summary>
    public string ExchangeName { get; set; } = "dead-letter-exchange";

    /// <summary>
    /// Whether to enable dead letter queue.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Time-to-live for messages in DLQ (null = infinite).
    /// </summary>
    public TimeSpan? MessageTtl { get; set; } = TimeSpan.FromDays(7);
}

/// <summary>
/// Represents a message that failed processing and was sent to DLQ.
/// </summary>
/// <typeparam name="T">The message type.</typeparam>
public class DeadLetterMessage<T> where T : class
{
    /// <summary>
    /// The original message envelope.
    /// </summary>
    public required MessageEnvelope<T> OriginalMessage { get; init; }

    /// <summary>
    /// Number of times the message was retried.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// The exception that caused the final failure.
    /// </summary>
    public string? LastException { get; init; }

    /// <summary>
    /// Timestamp when sent to DLQ.
    /// </summary>
    public DateTimeOffset SentToDlqAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The service that failed to process the message.
    /// </summary>
    public string? ConsumerName { get; init; }
}
