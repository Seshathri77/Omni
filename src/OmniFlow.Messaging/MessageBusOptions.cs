using OmniFlow.Core;

namespace OmniFlow.Messaging;

/// <summary>
/// Configuration options for the message bus adapter.
/// </summary>
public class MessageBusOptions
{
    /// <summary>
    /// The type of message bus to use (RabbitMQ, ServiceBus, Kafka, InMemory).
    /// </summary>
    public MessageBusProvider Provider { get; set; } = MessageBusProvider.InMemory;

    /// <summary>
    /// Service name for queue/topic naming.
    /// </summary>
    public string ServiceName { get; set; } = "default";

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

    /// <summary>
    /// Enable circuit breaker pattern.
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Circuit breaker failure ratio threshold (0.0 to 1.0).
    /// </summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Minimum requests before circuit breaker activates.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Sampling duration for calculating failure ratio (in seconds).
    /// </summary>
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// How long the circuit stays open before half-opening (in seconds).
    /// </summary>
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 30;

    /// <summary>
    /// RabbitMQ-specific configuration.
    /// </summary>
    public RabbitMQConfig? RabbitMQ { get; set; }

    /// <summary>
    /// Azure Service Bus-specific configuration.
    /// </summary>
    public ServiceBusConfig? ServiceBus { get; set; }

    /// <summary>
    /// Kafka-specific configuration.
    /// </summary>
    public KafkaConfig? Kafka { get; set; }
}

