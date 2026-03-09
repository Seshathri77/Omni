using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniFlow.Core;
using OmniFlow.Messaging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace OmniFlow.Adapters.Kafka;

/// <summary>
/// Kafka implementation of IMessageBus with consumer groups and offset management.
/// </summary>
public class KafkaMessageBus : IMessageBus, IDisposable
{
    private readonly KafkaOptions _options;
    private readonly ICorrelationAccessor _correlationAccessor;
    private readonly ILogger<KafkaMessageBus> _logger;
    private readonly IProducer<string, string> _producer;
    private readonly ConcurrentDictionary<Type, IConsumer<string, string>> _consumers = new();
    private readonly ConcurrentDictionary<Type, CancellationTokenSource> _consumerCancellations = new();

    public KafkaMessageBus(
        IOptions<KafkaOptions> options,
        ICorrelationAccessor correlationAccessor,
        ILogger<KafkaMessageBus> logger)
    {
        _options = options.Value;
        _correlationAccessor = correlationAccessor;
        _logger = logger;

        var producerConfig = CreateProducerConfig();
        _producer = new ProducerBuilder<string, string>(producerConfig).Build();

        _logger.LogInformation("Kafka Producer initialized for {BootstrapServers}", _options.BootstrapServers);
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        var envelope = MessageEnvelope<T>.Create(message, _correlationAccessor);
        await PublishAsync(envelope, cancellationToken);
    }

    public async Task PublishAsync<T>(MessageEnvelope<T> envelope, CancellationToken cancellationToken = default)
        where T : class
    {
        var topic = GetTopicName<T>();
        var json = JsonSerializer.Serialize(envelope);

        var kafkaMessage = new Message<string, string>
        {
            Key = envelope.CorrelationId,
            Value = json,
            Headers = new Headers
            {
                { "MessageId", Encoding.UTF8.GetBytes(envelope.MessageId) },
                { "CorrelationId", Encoding.UTF8.GetBytes(envelope.CorrelationId) },
                { "MessageType", Encoding.UTF8.GetBytes(typeof(T).Name) }
            }
        };

        try
        {
            var result = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
            
            _logger.LogDebug("Published message {MessageId} to topic {Topic} at offset {Offset}",
                envelope.MessageId, topic, result.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to publish message {MessageId} to topic {Topic}",
                envelope.MessageId, topic);
            throw;
        }
    }

    public Task SubscribeAsync<T>(
        Func<MessageEnvelope<T>, MessageContext, Task> handler,
        CancellationToken cancellationToken = default) where T : class
    {
        var topic = GetTopicName<T>();
        var consumerConfig = CreateConsumerConfig();

        var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(topic);

        _consumers[typeof(T)] = consumer;
        
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumerCancellations[typeof(T)] = cts;

        // Start consuming in background
        _ = Task.Run(async () => await ConsumeMessagesAsync<T>(consumer, handler, cts.Token), cts.Token);

        _logger.LogInformation("Subscribed to Kafka topic {Topic} with group {GroupId}",
            topic, _options.GroupId);

        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync<T>(CancellationToken cancellationToken = default) where T : class
    {
        if (_consumerCancellations.TryRemove(typeof(T), out var cts))
        {
            cts.Cancel();
        }

        if (_consumers.TryRemove(typeof(T), out var consumer))
        {
            consumer.Close();
            consumer.Dispose();
            
            _logger.LogInformation("Unsubscribed from topic {Topic}", GetTopicName<T>());
        }

        return Task.CompletedTask;
    }

    private async Task ConsumeMessagesAsync<T>(
        IConsumer<string, string> consumer,
        Func<MessageEnvelope<T>, MessageContext, Task> handler,
        CancellationToken cancellationToken) where T : class
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(cancellationToken);

                    if (consumeResult?.Message == null)
                        continue;

                    var envelope = JsonSerializer.Deserialize<MessageEnvelope<T>>(consumeResult.Message.Value);

                    if (envelope != null)
                    {
                        var context = MessageContext.FromEnvelope(envelope, cancellationToken);
                        
                        try
                        {
                            await handler(envelope, context);

                            // Commit offset on successful processing
                            if (!_options.EnableAutoCommit)
                            {
                                consumer.Commit(consumeResult);
                            }

                            _logger.LogDebug("Processed message {MessageId} from offset {Offset}",
                                envelope.MessageId, consumeResult.Offset);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing message {MessageId} from topic {Topic}",
                                envelope.MessageId, consumeResult.Topic);

                            // Don't commit offset on failure - message will be reprocessed
                            throw;
                        }
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private ProducerConfig CreateProducerConfig()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MaxInFlight = 5,
            MessageSendMaxRetries = 3
        };

        ApplySecurityConfig(config);
        return config;
    }

    private ConsumerConfig CreateConsumerConfig()
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            EnableAutoCommit = _options.EnableAutoCommit,
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_options.AutoOffsetReset, true),
            MaxPollIntervalMs = 300000,
            SessionTimeoutMs = 10000
        };

        ApplySecurityConfig(config);
        return config;
    }

    private void ApplySecurityConfig(ClientConfig config)
    {
        if (!string.IsNullOrEmpty(_options.SecurityProtocol))
        {
            config.SecurityProtocol = Enum.Parse<SecurityProtocol>(_options.SecurityProtocol, true);
        }

        if (!string.IsNullOrEmpty(_options.SaslMechanism))
        {
            config.SaslMechanism = Enum.Parse<SaslMechanism>(_options.SaslMechanism, true);
            config.SaslUsername = _options.SaslUsername;
            config.SaslPassword = _options.SaslPassword;
        }
    }

    private string GetTopicName<T>() => $"{_options.TopicPrefix}.{typeof(T).Name}".ToLowerInvariant();

    public void Dispose()
    {
        foreach (var cts in _consumerCancellations.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }

        foreach (var consumer in _consumers.Values)
        {
            consumer.Close();
            consumer.Dispose();
        }

        _producer?.Dispose();
        
        _logger.LogInformation("Kafka MessageBus disposed");
    }
}

/// <summary>
/// Configuration options for Kafka.
/// </summary>
public class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string GroupId { get; set; } = "omniflow";
    public string TopicPrefix { get; set; } = "omniflow";
    public bool EnableAutoCommit { get; set; } = false;
    public string AutoOffsetReset { get; set; } = "earliest";
    public int MaxPollRecords { get; set; } = 500;
    public string? SaslMechanism { get; set; }
    public string? SaslUsername { get; set; }
    public string? SaslPassword { get; set; }
    public string SecurityProtocol { get; set; } = "PLAINTEXT";
    public string ServiceName { get; set; } = "default";
}
