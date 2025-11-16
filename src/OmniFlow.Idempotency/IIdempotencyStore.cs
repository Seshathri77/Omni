namespace OmniFlow.Idempotency;

/// <summary>
/// Store for tracking processed message IDs to ensure idempotent message handling.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Attempts to record a message as processed atomically.
    /// </summary>
    /// <returns>True if the message was newly recorded (first time), false if already exists.</returns>
    Task<bool> TryRecordAsync(
        string messageId,
        string consumerName,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a message has already been processed.
    /// </summary>
    Task<bool> ExistsAsync(
        string messageId,
        string consumerName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a message record (for cleanup or testing).
    /// </summary>
    Task RemoveAsync(
        string messageId,
        string consumerName,
        CancellationToken cancellationToken = default);
}
