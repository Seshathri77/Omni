namespace OmniFlow.Core;

/// <summary>
/// Thread-safe correlation context accessor using AsyncLocal storage.
/// </summary>
public class CorrelationAccessor : ICorrelationAccessor
{
    private static readonly AsyncLocal<CorrelationContext> _context = new();

    /// <inheritdoc/>
    public string? CorrelationId => _context.Value?.CorrelationId;

    /// <inheritdoc/>
    public string? CausationId => _context.Value?.CausationId;

    /// <inheritdoc/>
    public void SetContext(string correlationId, string? causationId = null)
    {
        _context.Value = new CorrelationContext(correlationId, causationId);
    }

    private record CorrelationContext(string CorrelationId, string? CausationId);
}
