using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OmniFlow.Messaging;

namespace OmniFlow.Adapters.Kafka;

/// <summary>
/// Extension methods for registering Kafka adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kafka message bus using unified OmniFlow configuration.
    /// This method is automatically called when using AddOmniFlowMessageBus with MessageBusProvider.Kafka.
    /// </summary>
    public static IServiceCollection AddOmniFlowKafka(this IServiceCollection services)
    {
        services.AddSingleton<IMessageBus>(sp =>
        {
            var messageBusOptions = sp.GetRequiredService<IOptions<MessageBusOptions>>().Value;

            if (messageBusOptions.Kafka == null)
                throw new InvalidOperationException("Kafka configuration is required when using MessageBusProvider.Kafka");

            var kafkaOptions = Options.Create(new KafkaOptions
            {
                BootstrapServers = messageBusOptions.Kafka.BootstrapServers,
                GroupId = messageBusOptions.Kafka.GroupId,
                TopicPrefix = messageBusOptions.Kafka.TopicPrefix,
                EnableAutoCommit = messageBusOptions.Kafka.EnableAutoCommit,
                AutoOffsetReset = messageBusOptions.Kafka.AutoOffsetReset,
                MaxPollRecords = messageBusOptions.Kafka.MaxPollRecords,
                SaslMechanism = messageBusOptions.Kafka.SaslMechanism,
                SaslUsername = messageBusOptions.Kafka.SaslUsername,
                SaslPassword = messageBusOptions.Kafka.SaslPassword,
                SecurityProtocol = messageBusOptions.Kafka.SecurityProtocol,
                ServiceName = messageBusOptions.ServiceName
            });

            return new KafkaMessageBus(
                kafkaOptions,
                sp.GetRequiredService<Core.ICorrelationAccessor>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<KafkaMessageBus>>());
        });

        return services;
    }

    /// <summary>
    /// Adds Kafka message bus implementation.
    /// For backward compatibility. Consider using AddOmniFlowMessageBus with MessageBusProvider.Kafka instead.
    /// </summary>
    [Obsolete("Use AddOmniFlowMessageBus(options => options.Provider = MessageBusProvider.Kafka) instead.")]
    public static IServiceCollection AddKafkaMessageBus(
        this IServiceCollection services,
        Action<KafkaOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IMessageBus, KafkaMessageBus>();

        return services;
    }
}
