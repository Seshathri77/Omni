using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OmniFlow.Core;
using OmniFlow.Messaging.Middleware;
using OpenTelemetry.Trace;

namespace OmniFlow.Messaging;

/// <summary>
/// Extension methods for registering OmniFlow.Messaging services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all OmniFlow services with unified configuration.
    /// This is the recommended way to configure OmniFlow.
    /// </summary>
    public static IServiceCollection AddOmniFlow(
        this IServiceCollection services,
        Action<OmniFlowOptions> configure)
    {
        var options = new OmniFlowOptions();
        configure(options);

        services.Configure(configure);

        // Set service name on MessageBus options if not already set
        if (options.MessageBus.ServiceName == "default")
        {
            options.MessageBus.ServiceName = options.ServiceName;
        }

        // 1. Add Core services (always required)
        services.AddOmniFlowCore();

        // 2. Add Message Bus
        services.AddOmniFlowMessageBus(opt =>
        {
            opt.Provider = options.MessageBus.Provider;
            opt.ServiceName = options.MessageBus.ServiceName;
            opt.UseCorrelation = options.MessageBus.UseCorrelation;
            opt.UseLogging = options.MessageBus.UseLogging;
            opt.UseRetry = options.MessageBus.UseRetry;
            opt.MaxRetries = options.MessageBus.MaxRetries;

            // Circuit Breaker settings
            opt.EnableCircuitBreaker = options.MessageBus.EnableCircuitBreaker;
            opt.CircuitBreakerFailureRatio = options.MessageBus.CircuitBreakerFailureRatio;
            opt.CircuitBreakerMinimumThroughput = options.MessageBus.CircuitBreakerMinimumThroughput;
            opt.CircuitBreakerSamplingDurationSeconds = options.MessageBus.CircuitBreakerSamplingDurationSeconds;
            opt.CircuitBreakerBreakDurationSeconds = options.MessageBus.CircuitBreakerBreakDurationSeconds;

            // Adapter-specific configurations
            opt.RabbitMQ = options.MessageBus.RabbitMQ;
            opt.ServiceBus = options.MessageBus.ServiceBus;
            opt.Kafka = options.MessageBus.Kafka;
        });

        // Register provider-specific extensions
        switch (options.MessageBus.Provider)
        {
            case MessageBusProvider.RabbitMQ:
                // Use reflection to avoid hard dependency on RabbitMQ adapter
                var rabbitMQExtension = Type.GetType("OmniFlow.Adapters.RabbitMQ.ServiceCollectionExtensions, OmniFlow.Adapters.RabbitMQ");
                if (rabbitMQExtension != null)
                {
                    var method = rabbitMQExtension.GetMethod("AddOmniFlowRabbitMQ");
                    method?.Invoke(null, new object[] { services });
                }
                break;
            // Future: ServiceBus, Kafka
        }

        // 3. Add Sagas (if enabled)
        if (options.EnableSagas)
        {
            var sagasExtension = Type.GetType("OmniFlow.Sagas.ServiceCollectionExtensions, OmniFlow.Sagas");
            if (sagasExtension != null)
            {
                var addSagasMethod = sagasExtension.GetMethod("AddOmniFlowSagas");
                addSagasMethod?.Invoke(null, new object[] { services });

                // Register individual sagas
                foreach (var (sagaType, stateType) in options.SagaRegistrations)
                {
                    var addSagaMethod = sagasExtension.GetMethod("AddSaga");
                    var genericMethod = addSagaMethod?.MakeGenericMethod(sagaType, stateType);
                    genericMethod?.Invoke(null, new object[] { services });
                }

                // Add Outbox pattern if enabled
                if (options.EnableOutbox)
                {
                    var addOutboxMethod = sagasExtension.GetMethod("AddOmniFlowOutbox");
                    addOutboxMethod?.Invoke(null, new object[] { services });
                }
            }
        }

        // 4. Add Idempotency (if enabled)
        if (options.EnableIdempotency)
        {
            var idempotencyExtension = Type.GetType("OmniFlow.Idempotency.ServiceCollectionExtensions, OmniFlow.Idempotency");
            if (idempotencyExtension != null)
            {
                var method = idempotencyExtension.GetMethod("AddOmniFlowIdempotency");
                method?.Invoke(null, new object[] { services });
            }
        }

        // 5. Add Observability (if enabled)
        if (options.EnableObservability)
        {
            var observabilityExtension = Type.GetType("OmniFlow.Observability.ServiceCollectionExtensions, OmniFlow.Observability");
            if (observabilityExtension != null)
            {
                var method = observabilityExtension.GetMethod("AddOmniFlowObservability");

                // Configure tracing action
                Action<object>? tracingConfig = null;
                if (!string.IsNullOrEmpty(options.Observability.OtlpEndpoint))
                {
                    tracingConfig = builder =>
                    {
                        var tracerBuilder = builder as OpenTelemetry.Trace.TracerProviderBuilder;
                        tracerBuilder?.AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri(options.Observability.OtlpEndpoint);
                        });
                    };
                }
                else if (options.Observability.ConfigureTracing != null)
                {
                    tracingConfig = builder => options.Observability.ConfigureTracing(builder as OpenTelemetry.Trace.TracerProviderBuilder);
                }

                method?.Invoke(null, new object?[] 
                { 
                    services, 
                    options.ServiceName,
                    tracingConfig,
                    options.Observability.EnablePrometheusExporter 
                });
            }
        }

        return services;
    }

    /// <summary>
    /// Adds OmniFlow message bus with configurable provider (InMemory, RabbitMQ, ServiceBus, Kafka).
    /// This is the recommended way to configure message bus adapters.
    /// The actual adapter registration is delegated to provider-specific extensions.
    /// </summary>
    public static IServiceCollection AddOmniFlowMessageBus(
        this IServiceCollection services,
        Action<MessageBusOptions> configure)
    {
        var options = new MessageBusOptions();
        configure(options);

        services.Configure(configure);

        // Register middleware (common across all providers)
        services.AddSingleton<CorrelationMiddleware>();
        services.AddSingleton<LoggingMiddleware>();

        // For InMemory, we handle it directly since it's in this assembly
        if (options.Provider == MessageBusProvider.InMemory)
        {
            AddInMemoryMessageBus(services, options);
        }
        // For other providers, the options are configured and they should call
        // their specific extension methods that handle the registration

        return services;
    }

    /// <summary>
    /// Adds OmniFlow messaging services with an in-memory message bus.
    /// For backward compatibility. Consider using AddOmniFlowMessageBus instead.
    /// </summary>
    [Obsolete("Use AddOmniFlowMessageBus with MessageBusProvider.InMemory instead.")]
    public static IServiceCollection AddOmniFlowMessaging(
        this IServiceCollection services,
        Action<LegacyMessageBusOptions>? configure = null)
    {
        var options = new LegacyMessageBusOptions();
        configure?.Invoke(options);

        AddInMemoryMessageBus(services, new MessageBusOptions
        {
            UseCorrelation = options.UseCorrelation,
            UseLogging = options.UseLogging,
            UseRetry = options.UseRetry,
            MaxRetries = options.MaxRetries
        });

        services.AddSingleton<CorrelationMiddleware>();
        services.AddSingleton<LoggingMiddleware>();

        return services;
    }

    private static void AddInMemoryMessageBus(IServiceCollection services, MessageBusOptions options)
    {
        services.AddSingleton<InMemoryMessageBus>();
        services.AddSingleton<IMessageBus>(sp =>
        {
            var bus = sp.GetRequiredService<InMemoryMessageBus>();
            ApplyMiddleware(sp, bus, options);
            return bus;
        });
    }

    private static void ApplyMiddleware(IServiceProvider sp, InMemoryMessageBus bus, MessageBusOptions options)
    {
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
            var retryOptions = new RetryMiddlewareOptions
            {
                MaxRetries = options.MaxRetries,
                EnableCircuitBreaker = options.EnableCircuitBreaker,
                CircuitBreakerFailureRatio = options.CircuitBreakerFailureRatio,
                CircuitBreakerMinimumThroughput = options.CircuitBreakerMinimumThroughput
            };

            bus.UseMiddleware(new RetryMiddleware(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RetryMiddleware>>(),
                retryOptions));
        }
    }
}

/// <summary>
/// Legacy options for configuring the message bus.
/// Use MessageBusOptions with AddOmniFlowMessageBus instead.
/// </summary>
[Obsolete("Use MessageBusOptions with AddOmniFlowMessageBus instead.")]
public class LegacyMessageBusOptions
{
    public bool UseCorrelation { get; set; } = true;
    public bool UseLogging { get; set; } = true;
    public bool UseRetry { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
}
