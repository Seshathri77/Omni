using ECommerce.Contracts;
using ECommerce.PaymentService.Handlers;
using OmniFlow.Messaging;

namespace ECommerce.PaymentService.Extensions;

/// <summary>
/// Extension methods for registering payment message subscriptions
/// </summary>
public static class MessageSubscriptionExtensions
{
    /// <summary>
    /// Subscribes to all payment-related commands
    /// </summary>
    public static async Task SubscribeToPaymentCommandsAsync(this WebApplication app)
    {
        var messageBus = app.Services.GetRequiredService<IMessageBus>();

        // Payment request command
        await messageBus.SubscribeAsync<RequestPayment>(async (envelope, context) =>
        {
            using var scope = app.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<PaymentRequestHandler>();
            await handler.HandleRequestPayment(envelope, context, context.CancellationToken);
        });

        // Refund payment command  
        await messageBus.SubscribeAsync<RefundPayment>(async (envelope, context) =>
        {
            using var scope = app.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<RefundRequestHandler>();
            await handler.HandleRefundPayment(envelope, context, context.CancellationToken);
        });
    }

    /// <summary>
    /// Registers all payment handlers with DI
    /// </summary>
    public static IServiceCollection AddPaymentHandlers(this IServiceCollection services)
    {
        // Transient: New instance per message (stateless handlers)
        services.AddTransient<PaymentRequestHandler>();
        services.AddTransient<RefundRequestHandler>();

        return services;
    }
}
