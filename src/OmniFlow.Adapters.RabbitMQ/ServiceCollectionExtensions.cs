using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    /// Supports Aspire connection strings via ConnectionStrings__rabbitmq.
    /// </summary>
    public static IServiceCollection AddOmniFlowRabbitMQ(this IServiceCollection services)
    {
        // Register RabbitMQ message bus - configuration comes from MessageBusOptions or Aspire connection string
        services.AddSingleton<IMessageBus>(sp =>
        {
            var messageBusOptions = sp.GetRequiredService<IOptions<MessageBusOptions>>().Value;
            var configuration = sp.GetService<Microsoft.Extensions.Configuration.IConfiguration>();

            RabbitMQOptions rabbitOptions;

            // Check for Aspire connection string first (injected by Aspire AppHost)
            var aspireConnectionString = configuration?.GetConnectionString("rabbitmq");

            // DEBUG: Log what we're receiving
            if (!string.IsNullOrEmpty(aspireConnectionString))
            {
                var debugLogger = sp.GetRequiredService<ILogger<RabbitMQMessageBus>>();
                // Mask password for logging
                var maskedConnection = System.Text.RegularExpressions.Regex.Replace(
                    aspireConnectionString, 
                    @"(://[^:]+:)([^@]+)(@)", 
                    "$1****$3");
                debugLogger.LogWarning("🔍 DEBUG: Aspire connection string: {ConnectionString}", maskedConnection);
            }

            if (!string.IsNullOrEmpty(aspireConnectionString))
            {
                // Parse Aspire connection string (format: amqp://user:pass@host:port/vhost or amqp://host:port)
                var uri = new Uri(aspireConnectionString);

                // Parse username and password from UserInfo
                string userName = "guest";
                string password = "guest";

                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var userInfo = uri.UserInfo.Split(':');
                    if (userInfo.Length >= 1 && !string.IsNullOrEmpty(userInfo[0]))
                        userName = Uri.UnescapeDataString(userInfo[0]);
                    if (userInfo.Length >= 2 && !string.IsNullOrEmpty(userInfo[1]))
                        password = Uri.UnescapeDataString(userInfo[1]);
                }

                rabbitOptions = new RabbitMQOptions
                {
                    HostName = uri.Host,
                    Port = uri.Port > 0 ? uri.Port : 5672,
                    UserName = userName,
                    Password = password,
                    VirtualHost = string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/" 
                        ? "/" : uri.AbsolutePath.TrimStart('/'),
                    ExchangeName = messageBusOptions.RabbitMQ?.ExchangeName ?? "omniflow",
                    ServiceName = messageBusOptions.ServiceName,
                    DeadLetterQueue = messageBusOptions.RabbitMQ?.DeadLetterQueue != null
                        ? MapToDeadLetterQueueOptions(messageBusOptions.RabbitMQ.DeadLetterQueue)
                        : new DeadLetterQueueOptions()
                };

                // Log parsed configuration (without password)
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RabbitMQMessageBus>>();
                logger.LogInformation(
                    "Using Aspire RabbitMQ connection: Host={Host}, Port={Port}, User={User}, VirtualHost={VHost}",
                    rabbitOptions.HostName, rabbitOptions.Port, rabbitOptions.UserName, rabbitOptions.VirtualHost);
            }
            else if (messageBusOptions.RabbitMQ != null)
            {
                // Use configuration from appsettings.json
                rabbitOptions = new RabbitMQOptions
                {
                    HostName = messageBusOptions.RabbitMQ.HostName,
                    Port = messageBusOptions.RabbitMQ.Port,
                    UserName = messageBusOptions.RabbitMQ.UserName,
                    Password = messageBusOptions.RabbitMQ.Password,
                    VirtualHost = messageBusOptions.RabbitMQ.VirtualHost,
                    ExchangeName = messageBusOptions.RabbitMQ.ExchangeName,
                    ServiceName = messageBusOptions.ServiceName,
                    DeadLetterQueue = MapToDeadLetterQueueOptions(messageBusOptions.RabbitMQ.DeadLetterQueue)
                };
            }
            else
            {
                throw new InvalidOperationException(
                    "RabbitMQ configuration is required. Either provide RabbitMQ configuration in appsettings.json " +
                    "or ensure Aspire connection string (ConnectionStrings__rabbitmq) is available.");
            }

            return new RabbitMQMessageBus(
                Options.Create(rabbitOptions),
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
