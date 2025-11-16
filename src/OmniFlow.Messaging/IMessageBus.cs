using OmniFlow.Core;

namespace OmniFlow.Messaging;

/// <summary>
/// Abstraction for publishing and subscribing to messages across service boundaries.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes a message to the bus.
    /// </summary>
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Publishes a message envelope to the bus.
    /// </summary>
    Task PublishAsync<T>(MessageEnvelope<T> envelope, CancellationToken cancellationToken = default) 
        where T : class;

    /// <summary>
    /// Subscribes to messages of a specific type.
    /// </summary>
    Task SubscribeAsync<T>(Func<MessageEnvelope<T>, MessageContext, Task> handler, 
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Unsubscribes from messages of a specific type.
    /// </summary>
    Task UnsubscribeAsync<T>(CancellationToken cancellationToken = default) where T : class;
}
