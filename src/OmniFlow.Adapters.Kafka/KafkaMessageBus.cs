using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniFlow.Core;
using OmniFlow.Messaging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace OmniFlow.Adapters.Kafka;

/// <summary>
/// Kafka implementation of IMessageBus using Confluent.Kafka.
/// </summary>
public class KafkaMessageBus : IMessageBus, IDisposable
{
    private readonly KafkaOptions _options;
    private readonly ICorrelationAccessor _correlationAccessor;
    private readonly ILogger<KafkaMessageBus> _logger;
    private readonly IProducer<string, string> _producer;
    private readonly ConcurrentDictionary<Type, IConsumer<string, string>> _consumers = new();
    private readonly ConcurrentDictionary<Type, CancellationTokenSource> _consumerTasks = new();

    public KafkaMessageBus(
        IOptions<KafkaOptions> options,
        ICorrelationAccessor correlationAccessor,
        ILogger<KafkaMessageBus> logger)
    {
        _options = options.Value;
        _correlationAccessor = correlationAccessor;
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = _options.ClientId,
            Acks = Acks.All,
            EnableIdempotence = true,
            MaxInFlight = 5,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 100
        };

        // Add additional producer configuration
        if (_options.ProducerConfig != null)
        {
            foreach (var kvp in _options.ProducerConfig)
            {
                producerConfig.Set(kvp.Key, kvp.Value);
            }
        }

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        var envelope = MessageEnvelope<T>.Create(message, _correlationAccessor);
        return PublishAsync(envelope, cancellationToken);
    }

    public async Task PublishAsync<T>(MessageEnvelope<T> envelope, CancellationToken cancellationToken = default) 
        where T : class
    {
        var topic = GetTopicName<T>();
        var key = envelope.CorrelationId; // Use correlation ID as partition key for ordering
        var value = JsonSerializer.Serialize(envelope);

        var headers = new Headers
        {
            { "MessageId", System.Text.Encoding.UTF8.GetBytes(envelope.MessageId) },
            { "CorrelationId", System.Text.Encoding.UTF8.GetBytes(envelope.CorrelationId) },
            { "CausationId", System.Text.Encoding.UTF8.GetBytes(envelope.CausationId) },
            { "MessageType", System.Text.Encoding.UTF8.GetBytes(typeof(T).FullName ?? typeof(T).Name) }
        };

        var message = new Message<string, string>
        {
            Key = key,
            Value = value,
            Headers = headers,
            Timestamp = new Timestamp(envelope.Timestamp)
        };

        try
        {
            var result = await _producer.ProduceAsync(topic, message, cancellationToken);
            
            _logger.LogDebug(
                "Published message {MessageId} to topic {Topic} (Partition: {Partition}, Offset: {Offset})",
                envelope.MessageId, topic, result.Partition.Value, result.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Error publishing message {MessageId} to topic {Topic}", 
                envelope.MessageId, topic);
            throw;
        }
    }

    public Task SubscribeAsync<T>(
        Func<MessageEnvelope<T>, MessageContext, Task> handler,
        CancellationToken cancellationToken = default) where T : class
    {
        var messageType = typeof(T);
        
        if (_consumers.ContainsKey(messageType))
        {
            _logger.LogWarning("Already subscribed to {MessageType}", messageType.Name);
            return Task.CompletedTask;
        }

        var topic = GetTopicName<T>();
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            ClientId = $"{_options.ClientId}-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false, // Manual commit for reliable processing
            EnableAutoOffsetStore = false,
            MaxPollIntervalMs = 300000, // 5 minutes
            SessionTimeoutMs = 45000
        };

        // Add additional consumer configuration
        if (_options.ConsumerConfig != null)
        {
            foreach (var kvp in _options.ConsumerConfig)
            {
                consumerConfig.Set(kvp.Key, kvp.Value);
            }
        }

        var consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka error: {Reason}", e.Reason))
            .Build();

        consumer.Subscribe(topic);
        _consumers.TryAdd(messageType, consumer);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumerTasks.TryAdd(messageType, cts);

        // Start background consumption
        _ = Task.Run(async () =>
        {
            _logger.LogInformation("Started consuming from topic {Topic} with group {GroupId}", 
                topic, _options.ConsumerGroupId);

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = consumer.Consume(cts.Token);

                        if (consumeResult?.Message?.Value != null)
                        {
                            var envelope = JsonSerializer.Deserialize<MessageEnvelope<T>>(consumeResult.Message.Value);

                            if (envelope != null)
                            {
                                var context = MessageContext.FromEnvelope(envelope);
                                
                                try
                                {
                                    await handler(envelope, context);
                                    
                                    // Commit offset after successful processing
                                    consumer.Commit(consumeResult);
                                    consumer.StoreOffset(consumeResult);
                                    
                                    _logger.LogDebug(
                                        "Processed message {MessageId} from {Topic} (Partition: {Partition}, Offset: {Offset})",
                                        envelope.MessageId, topic, 
                                        consumeResult.Partition.Value, consumeResult.Offset.Value);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, 
                                        "Error processing message {MessageId} from {Topic}. Message will be retried.",
                                        envelope.MessageId, topic);
                                    
                                    // Don't commit offset on failure - message will be reprocessed
                                    // Consider implementing dead-letter topic for persistent failures
                                }
                            }
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming from topic {Topic}", topic);
                        await Task.Delay(1000, cts.Token); // Backoff on error
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer for topic {Topic} was cancelled", topic);
            }
            finally
            {
                consumer.Close();
            }
        }, cts.Token);

        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync<T>(CancellationToken cancellationToken = default) where T : class
    {
        var messageType = typeof(T);

        if (_consumerTasks.TryRemove(messageType, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        if (_consumers.TryRemove(messageType, out var consumer))
        {
            consumer.Dispose();
            _logger.LogInformation("Unsubscribed from {MessageType}", messageType.Name);
        }

        return Task.CompletedTask;
    }

    private string GetTopicName<T>()
    {
        var messageTypeName = typeof(T).Name.ToLowerInvariant();
        return string.IsNullOrEmpty(_options.TopicPrefix)
            ? messageTypeName
            : $"{_options.TopicPrefix}.{messageTypeName}";
    }

    public void Dispose()
    {
        // Cancel all consumer tasks
        foreach (var cts in _consumerTasks.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _consumerTasks.Clear();

        // Dispose all consumers
        foreach (var consumer in _consumers.Values)
        {
            consumer.Dispose();
        }
        _consumers.Clear();

        // Dispose producer
        _producer?.Dispose();
    }
}

/// <summary>
/// Configuration options for Kafka.
/// </summary>
public class KafkaOptions
{
    /// <summary>
    /// Kafka bootstrap servers (e.g., "localhost:9092" or "broker1:9092,broker2:9092").
    /// </summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// Client identifier for this application.
    /// </summary>
    public string ClientId { get; set; } = "omniflow-client";

    /// <summary>
    /// Consumer group ID for coordinated consumption.
    /// </summary>
    public string ConsumerGroupId { get; set; } = "omniflow-group";

    /// <summary>
    /// Optional topic prefix (e.g., "prod" results in topics like "prod.ordercreated").
    /// </summary>
    public string? TopicPrefix { get; set; }

    /// <summary>
    /// Additional producer configuration settings.
    /// </summary>
    public Dictionary<string, string>? ProducerConfig { get; set; }

    /// <summary>
    /// Additional consumer configuration settings.
    /// </summary>
    public Dictionary<string, string>? ConsumerConfig { get; set; }
}
