using Microsoft.Extensions.DependencyInjection;
using OmniFlow.Messaging;

namespace OmniFlow.Adapters.RabbitMQ;

/// <summary>
/// Extension methods for registering RabbitMQ adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds RabbitMQ message bus implementation.
    /// </summary>
    public static IServiceCollection AddRabbitMQMessageBus(
        this IServiceCollection services,
        Action<RabbitMQOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IMessageBus, RabbitMQMessageBus>();
        
        return services;
    }
}
