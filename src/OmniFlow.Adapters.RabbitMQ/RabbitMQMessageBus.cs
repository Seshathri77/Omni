using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniFlow.Core;
using OmniFlow.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace OmniFlow.Adapters.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of IMessageBus with Dead Letter Queue support.
/// </summary>
public class RabbitMQMessageBus : IMessageBus, IDisposable
{
    private readonly RabbitMQOptions _options;
    private readonly ICorrelationAccessor _correlationAccessor;
    private readonly ILogger<RabbitMQMessageBus> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private const string RetryCountHeader = "x-retry-count";
    private const string OriginalExceptionHeader = "x-original-exception";

    public RabbitMQMessageBus(
        IOptions<RabbitMQOptions> options,
        ICorrelationAccessor correlationAccessor,
        ILogger<RabbitMQMessageBus> logger)
    {
        _options = options.Value;
        _correlationAccessor = correlationAccessor;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        DeclareExchange();

        if (_options.DeadLetterQueue.Enabled)
        {
            DeclareDeadLetterQueue();
        }
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        var envelope = MessageEnvelope<T>.Create(message, _correlationAccessor);
        return PublishAsync(envelope, cancellationToken);
    }

    public Task PublishAsync<T>(MessageEnvelope<T> envelope, CancellationToken cancellationToken = default) 
        where T : class
    {
        var routingKey = GetRoutingKey<T>();
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.MessageId = envelope.MessageId;
        properties.CorrelationId = envelope.CorrelationId;
        properties.Timestamp = new AmqpTimestamp(envelope.Timestamp.ToUnixTimeSeconds());

        _channel.BasicPublish(
            exchange: _options.ExchangeName,
            routingKey: routingKey,
            basicProperties: properties,
            body: body);

        _logger.LogDebug("Published message {MessageId} to {RoutingKey}", envelope.MessageId, routingKey);
        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(
        Func<MessageEnvelope<T>, MessageContext, Task> handler,
        CancellationToken cancellationToken = default) where T : class
    {
        var queueName = GetQueueName<T>();
        var routingKey = GetRoutingKey<T>();

        // Declare queue with DLQ configuration
        var queueArgs = new Dictionary<string, object>();
        if (_options.DeadLetterQueue.Enabled)
        {
            queueArgs["x-dead-letter-exchange"] = _options.DeadLetterQueue.ExchangeName;
        }

        _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs);
        _channel.QueueBind(queueName, _options.ExchangeName, routingKey);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var retryCount = GetRetryCount(ea.BasicProperties);

            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var envelope = JsonSerializer.Deserialize<MessageEnvelope<T>>(json);

                if (envelope != null)
                {
                    var context = MessageContext.FromEnvelope(envelope, cancellationToken);
                    await handler(envelope, context);
                    _channel.BasicAck(ea.DeliveryTag, false);

                    _logger.LogDebug("Successfully processed message {MessageId}", envelope.MessageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message (retry {RetryCount}/{MaxRetries})", 
                    retryCount, _options.DeadLetterQueue.MaxRetries);

                if (_options.DeadLetterQueue.Enabled && retryCount >= _options.DeadLetterQueue.MaxRetries)
                {
                    // Max retries exceeded - send to DLQ
                    await SendToDeadLetterQueueAsync(ea.Body.ToArray(), ea.BasicProperties, ex, retryCount);
                    _channel.BasicAck(ea.DeliveryTag, false); // Acknowledge to remove from queue

                    _logger.LogWarning("Message sent to DLQ after {RetryCount} failed attempts", retryCount);
                }
                else
                {
                    // Increment retry count and requeue
                    var updatedProperties = ClonePropertiesWithIncrementedRetry(ea.BasicProperties, ex);

                    // Reject and requeue with updated headers
                    _channel.BasicNack(ea.DeliveryTag, false, false);

                    // Republish with updated retry count
                    _channel.BasicPublish(
                        exchange: _options.ExchangeName,
                        routingKey: ea.RoutingKey,
                        basicProperties: updatedProperties,
                        body: ea.Body);

                    _logger.LogInformation("Message requeued with retry count {RetryCount}", retryCount + 1);
                }
            }
        };

        _channel.BasicConsume(queueName, autoAck: false, consumer);
        _logger.LogInformation("Subscribed to {QueueName} with DLQ support", queueName);

        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync<T>(CancellationToken cancellationToken = default) where T : class
    {
        // RabbitMQ doesn't require explicit unsubscribe
        return Task.CompletedTask;
    }

    private void DeclareExchange()
    {
        _channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Topic, durable: true);
    }

    private void DeclareDeadLetterQueue()
    {
        // Declare DLQ exchange
        _channel.ExchangeDeclare(_options.DeadLetterQueue.ExchangeName, ExchangeType.Fanout, durable: true);

        // Declare DLQ queue with TTL if configured
        var dlqArgs = new Dictionary<string, object>();
        if (_options.DeadLetterQueue.MessageTtl.HasValue)
        {
            dlqArgs["x-message-ttl"] = (int)_options.DeadLetterQueue.MessageTtl.Value.TotalMilliseconds;
        }

        _channel.QueueDeclare(_options.DeadLetterQueue.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: dlqArgs);
        _channel.QueueBind(_options.DeadLetterQueue.QueueName, _options.DeadLetterQueue.ExchangeName, "");

        _logger.LogInformation("Dead Letter Queue '{QueueName}' configured", _options.DeadLetterQueue.QueueName);
    }

    private async Task SendToDeadLetterQueueAsync(byte[] body, IBasicProperties originalProperties, Exception exception, int retryCount)
    {
        var dlqProperties = _channel.CreateBasicProperties();
        dlqProperties.Persistent = true;
        dlqProperties.MessageId = originalProperties.MessageId;
        dlqProperties.CorrelationId = originalProperties.CorrelationId;
        dlqProperties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        dlqProperties.Headers = new Dictionary<string, object>
        {
            [RetryCountHeader] = retryCount,
            [OriginalExceptionHeader] = exception.ToString(),
            ["x-service-name"] = _options.ServiceName,
            ["x-sent-to-dlq-at"] = DateTimeOffset.UtcNow.ToString("O")
        };

        _channel.BasicPublish(
            exchange: _options.DeadLetterQueue.ExchangeName,
            routingKey: "",
            basicProperties: dlqProperties,
            body: body);

        await Task.CompletedTask;
    }

    private int GetRetryCount(IBasicProperties properties)
    {
        if (properties?.Headers != null && properties.Headers.TryGetValue(RetryCountHeader, out var value))
        {
            return value is int count ? count : 0;
        }
        return 0;
    }

    private IBasicProperties ClonePropertiesWithIncrementedRetry(IBasicProperties original, Exception exception)
    {
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = original.Persistent;
        properties.MessageId = original.MessageId;
        properties.CorrelationId = original.CorrelationId;
        properties.Timestamp = original.Timestamp;

        var retryCount = GetRetryCount(original) + 1;
        properties.Headers = new Dictionary<string, object>
        {
            [RetryCountHeader] = retryCount,
            [OriginalExceptionHeader] = exception.Message
        };

        // Copy existing headers except retry count
        if (original.Headers != null)
        {
            foreach (var header in original.Headers.Where(h => h.Key != RetryCountHeader))
            {
                properties.Headers[header.Key] = header.Value;
            }
        }

        return properties;
    }

    private string GetRoutingKey<T>() => typeof(T).Name.ToLowerInvariant();
    private string GetQueueName<T>() => $"{_options.ServiceName}.{typeof(T).Name}";

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}

/// <summary>
/// Configuration options for RabbitMQ.
/// </summary>
public class RabbitMQOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "omniflow";
    public string ServiceName { get; set; } = "default";

    /// <summary>
    /// Dead Letter Queue configuration.
    /// </summary>
    public DeadLetterQueueOptions DeadLetterQueue { get; set; } = new();
}
