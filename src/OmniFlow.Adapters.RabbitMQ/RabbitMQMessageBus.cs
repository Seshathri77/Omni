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
/// RabbitMQ implementation of IMessageBus.
/// </summary>
public class RabbitMQMessageBus : IMessageBus, IDisposable
{
    private readonly RabbitMQOptions _options;
    private readonly ICorrelationAccessor _correlationAccessor;
    private readonly ILogger<RabbitMQMessageBus> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;

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

        _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(queueName, _options.ExchangeName, routingKey);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var envelope = JsonSerializer.Deserialize<MessageEnvelope<T>>(json);

                if (envelope != null)
                {
                    var context = MessageContext.FromEnvelope(envelope);
                    await handler(envelope, context);
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(queueName, autoAck: false, consumer);
        _logger.LogInformation("Subscribed to {QueueName}", queueName);

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
}
