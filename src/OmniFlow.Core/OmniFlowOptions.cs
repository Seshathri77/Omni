using OpenTelemetry.Trace;

namespace OmniFlow.Core;

/// <summary>
/// Unified configuration options for OmniFlow framework.
/// </summary>
public class OmniFlowOptions
{
    /// <summary>
    /// Service name used across all components (message bus, observability, etc.).
    /// </summary>
    public string ServiceName { get; set; } = "DefaultService";

    /// <summary>
    /// Message bus configuration.
    /// </summary>
    public MessageBusConfiguration MessageBus { get; set; } = new();

    /// <summary>
    /// Enable saga orchestration. Default is true.
    /// </summary>
    public bool EnableSagas { get; set; } = true;

    /// <summary>
    /// Enable outbox pattern for transactional messaging. Default is false.
    /// Requires EnableSagas to be true.
    /// </summary>
    public bool EnableOutbox { get; set; } = false;

    /// <summary>
    /// Enable idempotency store. Default is true.
    /// </summary>
    public bool EnableIdempotency { get; set; } = true;

    /// <summary>
    /// Enable observability (OpenTelemetry, metrics, tracing). Default is true.
    /// </summary>
    public bool EnableObservability { get; set; } = true;

    /// <summary>
    /// Observability configuration.
    /// </summary>
    public ObservabilityOptions Observability { get; set; } = new();

    /// <summary>
    /// Saga types to register. Use RegisterSaga<TSaga, TState>() to add sagas.
    /// </summary>
    public List<(Type SagaType, Type StateType)> SagaRegistrations { get; } = new();

    /// <summary>
    /// Register a saga type for dependency injection.
    /// </summary>
    public OmniFlowOptions RegisterSaga<TSaga, TState>()
        where TSaga : class
        where TState : class
    {
        SagaRegistrations.Add((typeof(TSaga), typeof(TState)));
        return this;
    }
}

/// <summary>
/// Message bus configuration.
/// </summary>
public class MessageBusConfiguration
{
    public string ServiceName { get; set; } = "default";
    public MessageBusProvider Provider { get; set; } = MessageBusProvider.InMemory;

    // Middleware settings
    public bool UseCorrelation { get; set; } = true;
    public bool UseLogging { get; set; } = true;
    public bool UseRetry { get; set; } = true;
    public int MaxRetries { get; set; } = 3;

    // Circuit Breaker settings
    public bool EnableCircuitBreaker { get; set; } = true;
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 30;

    // Adapter-specific configurations
    public RabbitMQConfig? RabbitMQ { get; set; }
    public ServiceBusConfig? ServiceBus { get; set; }
    public KafkaConfig? Kafka { get; set; }
}

/// <summary>
/// Message bus provider types.
/// </summary>
public enum MessageBusProvider
{
    InMemory,
    RabbitMQ,
    ServiceBus,
    Kafka
}

/// <summary>
/// RabbitMQ-specific configuration.
/// </summary>
public class RabbitMQConfig
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "omniflow";

    /// <summary>
    /// Dead Letter Queue configuration.
    /// </summary>
    public DeadLetterQueueConfig DeadLetterQueue { get; set; } = new();
}

/// <summary>
/// Dead Letter Queue configuration.
/// </summary>
public class DeadLetterQueueConfig
{
    /// <summary>
    /// Maximum number of retry attempts before sending to DLQ.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Dead letter queue name.
    /// </summary>
    public string QueueName { get; set; } = "dead-letter-queue";

    /// <summary>
    /// Dead letter exchange name.
    /// </summary>
    public string ExchangeName { get; set; } = "dead-letter-exchange";

    /// <summary>
    /// Whether to enable dead letter queue.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Time-to-live for messages in DLQ (null = infinite).
    /// </summary>
    public TimeSpan? MessageTtl { get; set; } = TimeSpan.FromDays(7);
}

/// <summary>
/// Azure Service Bus-specific configuration.
/// </summary>
public class ServiceBusConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public string TopicName { get; set; } = "omniflow";
}

/// <summary>
/// Kafka-specific configuration.
/// </summary>
public class KafkaConfig
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string GroupId { get; set; } = "omniflow";
    public string TopicPrefix { get; set; } = "omniflow";
}

/// <summary>
/// Observability configuration options.
/// </summary>
public class ObservabilityOptions
{
    /// <summary>
    /// Enable Prometheus metrics exporter. Default is true.
    /// </summary>
    public bool EnablePrometheusExporter { get; set; } = true;

    /// <summary>
    /// Optional tracing configuration (e.g., OTLP exporter to Jaeger).
    /// </summary>
    public Action<TracerProviderBuilder>? ConfigureTracing { get; set; }

    /// <summary>
    /// Configure OTLP exporter endpoint (e.g., for Jaeger).
    /// </summary>
    public string? OtlpEndpoint { get; set; }
}
