using Microsoft.Extensions.DependencyInjection;
using OmniFlow.Core;
using OmniFlow.Messaging;

namespace OmniFlow.Sagas;

/// <summary>
/// Extension methods for simplifying saga message subscriptions.
/// </summary>
public static class SagaSubscriptionExtensions
{
    /// <summary>
    /// Subscribes to a message that starts a new saga.
    /// </summary>
    public static async Task SubscribeSagaStart<TSaga, TState, TMessage>(
        this IMessageBus messageBus,
        IServiceProvider serviceProvider,
        Func<TSaga, TMessage, CancellationToken, Task> handler)
        where TSaga : Saga<TState>
        where TState : SagaState, new()
        where TMessage : class, IMessage
    {
        var repository = serviceProvider.GetRequiredService<ISagaRepository<TState>>();
        var timerService = serviceProvider.GetService<ITimerService>();

        await messageBus.SubscribeAsync<TMessage>(async (envelope, context) =>
        {
            var saga = ActivatorUtilities.CreateInstance<TSaga>(serviceProvider);
            saga.Initialize(repository, messageBus, timerService);
            await handler(saga, envelope.Message, CancellationToken.None);
        });
    }

    /// <summary>
    /// Subscribes to a message that continues an existing saga.
    /// </summary>
    public static async Task SubscribeSagaContinue<TSaga, TState, TMessage>(
        this IMessageBus messageBus,
        IServiceProvider serviceProvider,
        Func<TMessage, string> getSagaId,
        Func<TSaga, TMessage, CancellationToken, Task> handler)
        where TSaga : Saga<TState>
        where TState : SagaState, new()
        where TMessage : class, IMessage
    {
        var repository = serviceProvider.GetRequiredService<ISagaRepository<TState>>();
        var timerService = serviceProvider.GetService<ITimerService>();

        await messageBus.SubscribeAsync<TMessage>(async (envelope, context) =>
        {
            var saga = ActivatorUtilities.CreateInstance<TSaga>(serviceProvider);
            saga.Initialize(repository, messageBus, timerService);

            var sagaId = getSagaId(envelope.Message);
            if (await saga.LoadAsync(sagaId, CancellationToken.None))
            {
                await handler(saga, envelope.Message, CancellationToken.None);
            }
        });
    }
}
