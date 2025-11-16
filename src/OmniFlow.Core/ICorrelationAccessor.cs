namespace OmniFlow.Core;

/// <summary>
/// Provides access to the current correlation context.
/// </summary>
public interface ICorrelationAccessor
{
    /// <summary>
    /// Gets the correlation ID for the current operation.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Gets the causation ID (ID of the message that caused this operation).
    /// </summary>
    string? CausationId { get; }

    /// <summary>
    /// Sets the correlation context for the current operation.
    /// </summary>
    void SetContext(string correlationId, string? causationId = null);
}
