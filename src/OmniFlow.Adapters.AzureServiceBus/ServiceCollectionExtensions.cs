using Microsoft.Extensions.DependencyInjection;
using OmniFlow.Messaging;

namespace OmniFlow.Adapters.AzureServiceBus;

/// <summary>
/// Extension methods for registering Azure Service Bus adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Azure Service Bus message bus implementation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for ServiceBusOptions.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAzureServiceBusMessageBus(
        this IServiceCollection services,
        Action<ServiceBusOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IMessageBus, AzureServiceBusMessageBus>();
        
        return services;
    }
}
