using ECommerce.Contracts;
using ECommerce.OrderService.Sagas;
using OmniFlow.Messaging;
using OmniFlow.Sagas;

namespace ECommerce.OrderService.Extensions;

/// <summary>
/// Extension methods for registering message subscriptions using OmniFlow Saga framework
/// </summary>
public static class MessageSubscriptionExtensions
{
    /// <summary>
    /// Subscribes to all saga-related events using OmniFlow framework
    /// </summary>
    public static async Task SubscribeToSagaEventsAsync(this WebApplication app)
    {
        var messageBus = app.Services.GetRequiredService<IMessageBus>();

        // Inventory events - Continue saga
        await messageBus.SubscribeSagaContinue<OrderSaga, OrderSagaState, InventoryReserved>(
            app.Services,
            msg => msg.OrderId,
            async (saga, msg, ct) => await saga.HandleInventoryReserved(msg, ct));

        await messageBus.SubscribeSagaContinue<OrderSaga, OrderSagaState, InventoryReservationFailed>(
            app.Services,
            msg => msg.OrderId,
            async (saga, msg, ct) => await saga.HandleInventoryReservationFailed(msg, ct));

        // Payment events - Continue saga
        await messageBus.SubscribeSagaContinue<OrderSaga, OrderSagaState, PaymentSucceeded>(
            app.Services,
            msg => msg.OrderId,
            async (saga, msg, ct) => await saga.HandlePaymentSucceeded(msg, ct));

        await messageBus.SubscribeSagaContinue<OrderSaga, OrderSagaState, PaymentFailed>(
            app.Services,
            msg => msg.OrderId,
            async (saga, msg, ct) => await saga.HandlePaymentFailed(msg, ct));

        // Order events - Continue saga
        await messageBus.SubscribeSagaContinue<OrderSaga, OrderSagaState, OrderShipped>(
            app.Services,
            msg => msg.OrderId,
            async (saga, msg, ct) => await saga.HandleOrderShipped(msg, ct));

        // Cancel order command - Continue saga and trigger compensation
        await messageBus.SubscribeSagaContinue<OrderSaga, OrderSagaState, CancelOrder>(
            app.Services,
            msg => msg.OrderId,
            async (saga, msg, ct) => await saga.HandleCancelOrder(msg, ct));
    }
}
