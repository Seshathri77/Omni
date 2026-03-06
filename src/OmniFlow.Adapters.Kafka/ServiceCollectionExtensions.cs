using Microsoft.Extensions.DependencyInjection;
using OmniFlow.Messaging;

namespace OmniFlow.Adapters.Kafka;

/// <summary>
/// Extension methods for registering Kafka adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kafka message bus implementation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for KafkaOptions.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKafkaMessageBus(
        this IServiceCollection services,
        Action<KafkaOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IMessageBus, KafkaMessageBus>();
        
        return services;
    }
}
