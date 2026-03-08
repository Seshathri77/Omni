using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OmniFlow.Messaging;

namespace OmniFlow.Adapters.ServiceBus;

/// <summary>
/// Extension methods for registering Azure Service Bus adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Azure Service Bus message bus using unified OmniFlow configuration.
    /// This method is automatically called when using AddOmniFlowMessageBus with MessageBusProvider.ServiceBus.
    /// </summary>
    public static IServiceCollection AddOmniFlowServiceBus(this IServiceCollection services)
    {
        services.AddSingleton<IMessageBus>(sp =>
        {
            var messageBusOptions = sp.GetRequiredService<IOptions<MessageBusOptions>>().Value;

            if (messageBusOptions.ServiceBus == null)
                throw new InvalidOperationException("Service Bus configuration is required when using MessageBusProvider.ServiceBus");

            var serviceBusOptions = Options.Create(new ServiceBusOptions
            {
                ConnectionString = messageBusOptions.ServiceBus.ConnectionString,
                TopicName = messageBusOptions.ServiceBus.TopicName,
                SubscriptionName = messageBusOptions.ServiceBus.SubscriptionName,
                MaxDeliveryCount = messageBusOptions.ServiceBus.MaxDeliveryCount,
                MessageTimeToLive = messageBusOptions.ServiceBus.MessageTimeToLive,
                ServiceName = messageBusOptions.ServiceName
            });

            return new ServiceBusMessageBus(
                serviceBusOptions,
                sp.GetRequiredService<Core.ICorrelationAccessor>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ServiceBusMessageBus>>());
        });

        return services;
    }

    /// <summary>
    /// Adds Azure Service Bus message bus implementation.
    /// For backward compatibility. Consider using AddOmniFlowMessageBus with MessageBusProvider.ServiceBus instead.
    /// </summary>
    [Obsolete("Use AddOmniFlowMessageBus(options => options.Provider = MessageBusProvider.ServiceBus) instead.")]
    public static IServiceCollection AddServiceBusMessageBus(
        this IServiceCollection services,
        Action<ServiceBusOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IMessageBus, ServiceBusMessageBus>();

        return services;
    }
}
