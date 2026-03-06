using Microsoft.Extensions.DependencyInjection;
using OmniFlow.Messaging.Middleware;

namespace OmniFlow.Messaging;

/// <summary>
/// Extension methods for registering OmniFlow.Messaging services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OmniFlow messaging services with an in-memory message bus.
    /// </summary>
    public static IServiceCollection AddOmniFlowMessaging(
        this IServiceCollection services,
        Action<MessageBusOptions>? configure = null)
    {
        var options = new MessageBusOptions();
        configure?.Invoke(options);

        // Register message bus
        services.AddSingleton<IMessageBus>(sp =>
        {
            var bus = sp.GetRequiredService<InMemoryMessageBus>();
            
            // Add configured middleware
            if (options.UseCorrelation)
            {
                bus.UseMiddleware(sp.GetRequiredService<CorrelationMiddleware>());
            }
            if (options.UseLogging)
            {
                bus.UseMiddleware(sp.GetRequiredService<LoggingMiddleware>());
            }
            if (options.UseRetry)
            {
                bus.UseMiddleware(new RetryMiddleware(
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RetryMiddleware>>(),
                    options.MaxRetries));
            }
            
            return bus;
        });

        services.AddSingleton<InMemoryMessageBus>();

        // Register middleware
        services.AddSingleton<CorrelationMiddleware>();
        services.AddSingleton<LoggingMiddleware>();

        return services;
    }

    /// <summary>
    /// Adds Dead Letter Queue processing with automatic retry.
    /// Requires IDeadLetterQueueStore to be registered (e.g., via AddOmniFlowSqlAdapters).
    /// </summary>
    public static IServiceCollection AddDeadLetterQueueProcessor(
        this IServiceCollection services,
        Action<DeadLetterQueueOptions>? configure = null)
    {
        var options = new DeadLetterQueueOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddHostedService<DeadLetterQueueProcessor>();

        return services;
    }
}

/// <summary>
/// Options for configuring the message bus.
/// </summary>
public class MessageBusOptions
{
    /// <summary>
    /// Enable correlation middleware.
    /// </summary>
    public bool UseCorrelation { get; set; } = true;

    /// <summary>
    /// Enable logging middleware.
    /// </summary>
    public bool UseLogging { get; set; } = true;

    /// <summary>
    /// Enable retry middleware.
    /// </summary>
    public bool UseRetry { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
