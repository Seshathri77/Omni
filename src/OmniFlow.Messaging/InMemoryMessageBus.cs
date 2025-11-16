using Microsoft.Extensions.Logging;
using OmniFlow.Core;
using System.Collections.Concurrent;

namespace OmniFlow.Messaging;

/// <summary>
/// In-memory message bus implementation for testing and development.
/// </summary>
public class InMemoryMessageBus : IMessageBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _subscriptions = new();
    private readonly ICorrelationAccessor _correlationAccessor;
    private readonly ILogger<InMemoryMessageBus> _logger;
    private readonly List<IMessageMiddleware> _middlewares = new();

    public InMemoryMessageBus(
        ICorrelationAccessor correlationAccessor,
        ILogger<InMemoryMessageBus> logger)
    {
        _correlationAccessor = correlationAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Adds middleware to the processing pipeline.
    /// </summary>
    public void UseMiddleware(IMessageMiddleware middleware)
    {
        _middlewares.Add(middleware);
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        var envelope = MessageEnvelope<T>.Create(message, _correlationAccessor);
        return PublishAsync(envelope, cancellationToken);
    }

    public async Task PublishAsync<T>(MessageEnvelope<T> envelope, CancellationToken cancellationToken = default) 
        where T : class
    {
        var messageType = typeof(T);
        
        if (!_subscriptions.TryGetValue(messageType, out var handlers) || handlers.Count == 0)
        {
            _logger.LogWarning("No handlers subscribed for message type {MessageType}", messageType.Name);
            return;
        }

        var context = MessageContext.FromEnvelope(envelope);

        foreach (var handler in handlers.ToList())
        {
            var typedHandler = (Func<MessageEnvelope<T>, MessageContext, Task>)handler;
            await ExecuteWithMiddleware(envelope, context, () => typedHandler(envelope, context), cancellationToken);
        }
    }

    public Task SubscribeAsync<T>(
        Func<MessageEnvelope<T>, MessageContext, Task> handler, 
        CancellationToken cancellationToken = default) where T : class
    {
        var messageType = typeof(T);
        var handlers = _subscriptions.GetOrAdd(messageType, _ => new List<Delegate>());
        
        lock (handlers)
        {
            handlers.Add(handler);
        }

        _logger.LogInformation("Subscribed handler for message type {MessageType}", messageType.Name);
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync<T>(CancellationToken cancellationToken = default) where T : class
    {
        var messageType = typeof(T);
        _subscriptions.TryRemove(messageType, out _);
        
        _logger.LogInformation("Unsubscribed all handlers for message type {MessageType}", messageType.Name);
        return Task.CompletedTask;
    }

    private async Task ExecuteWithMiddleware<T>(
        MessageEnvelope<T> envelope,
        MessageContext context,
        Func<Task> handler,
        CancellationToken cancellationToken) where T : class
    {
        Func<Task> pipeline = handler;

        // Build middleware pipeline in reverse order
        for (int i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var next = pipeline;
            pipeline = () => middleware.InvokeAsync(envelope, context, next, cancellationToken);
        }

        await pipeline();
    }
}
