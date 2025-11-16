using OmniFlow.Core;

namespace OmniFlow.Messaging;

/// <summary>
/// Middleware for processing messages in a pipeline.
/// </summary>
public interface IMessageMiddleware
{
    /// <summary>
    /// Processes a message and optionally invokes the next middleware.
    /// </summary>
    Task InvokeAsync<T>(
        MessageEnvelope<T> envelope, 
        MessageContext context, 
        Func<Task> next,
        CancellationToken cancellationToken = default) where T : class;
}
