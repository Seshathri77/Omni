using Microsoft.Extensions.Logging;
using OmniFlow.Core;

namespace OmniFlow.Messaging.Middleware;

/// <summary>
/// Middleware that sets the correlation context for message processing.
/// </summary>
public class CorrelationMiddleware : IMessageMiddleware
{
    private readonly ICorrelationAccessor _correlationAccessor;
    private readonly ILogger<CorrelationMiddleware> _logger;

    public CorrelationMiddleware(
        ICorrelationAccessor correlationAccessor,
        ILogger<CorrelationMiddleware> logger)
    {
        _correlationAccessor = correlationAccessor;
        _logger = logger;
    }

    public async Task InvokeAsync<T>(
        MessageEnvelope<T> envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default) where T : class
    {
        _correlationAccessor.SetContext(envelope.CorrelationId, envelope.CausationId);

        _logger.LogDebug(
            "Processing message {MessageType} with CorrelationId: {CorrelationId}, CausationId: {CausationId}",
            envelope.MessageType,
            envelope.CorrelationId,
            envelope.CausationId);

        await next();
    }
}
