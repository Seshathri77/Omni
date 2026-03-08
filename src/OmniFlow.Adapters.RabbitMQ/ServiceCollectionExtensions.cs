using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OmniFlow.Messaging;

namespace OmniFlow.Adapters.RabbitMQ;

/// <summary>
/// Extension methods for registering RabbitMQ adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds RabbitMQ message bus using unified OmniFlow configuration.
    /// This method is automatically called when using AddOmniFlowMessageBus with MessageBusProvider.RabbitMQ.
    /// </summary>
    public static IServiceCollection AddOmniFlowRabbitMQ(this IServiceCollection services)
    {
        // Register RabbitMQ message bus - configuration comes from MessageBusOptions
        services.AddSingleton<IMessageBus>(sp =>
        {
            var messageBusOptions = sp.GetRequiredService<IOptions<MessageBusOptions>>().Value;

            if (messageBusOptions.RabbitMQ == null)
                throw new InvalidOperationException("RabbitMQ configuration is required when using MessageBusProvider.RabbitMQ");

            var rabbitOptions = Options.Create(new RabbitMQOptions
            {
                HostName = messageBusOptions.RabbitMQ.HostName,
                Port = messageBusOptions.RabbitMQ.Port,
                UserName = messageBusOptions.RabbitMQ.UserName,
                Password = messageBusOptions.RabbitMQ.Password,
                VirtualHost = messageBusOptions.RabbitMQ.VirtualHost,
                ExchangeName = messageBusOptions.RabbitMQ.ExchangeName,
                ServiceName = messageBusOptions.ServiceName,
                DeadLetterQueue = MapToDeadLetterQueueOptions(messageBusOptions.RabbitMQ.DeadLetterQueue)
            });

            return new RabbitMQMessageBus(
                rabbitOptions,
                sp.GetRequiredService<Core.ICorrelationAccessor>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RabbitMQMessageBus>>());
        });

        return services;
    }

    private static DeadLetterQueueOptions MapToDeadLetterQueueOptions(Core.DeadLetterQueueConfig config)
    {
        return new DeadLetterQueueOptions
        {
            Enabled = config.Enabled,
            MaxRetries = config.MaxRetries,
            QueueName = config.QueueName,
            ExchangeName = config.ExchangeName,
            MessageTtl = config.MessageTtl
        };
    }

    /// <summary>
    /// Adds RabbitMQ message bus implementation.
    /// For backward compatibility. Consider using AddOmniFlowMessageBus with MessageBusProvider.RabbitMQ instead.
    /// </summary>
    [Obsolete("Use AddOmniFlowMessageBus(options => options.Provider = MessageBusProvider.RabbitMQ) instead.")]
    public static IServiceCollection AddRabbitMQMessageBus(
        this IServiceCollection services,
        Action<RabbitMQOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IMessageBus, RabbitMQMessageBus>();

        return services;
    }
}
