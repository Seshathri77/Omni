using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniFlow.Core;
using OmniFlow.Messaging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace OmniFlow.Adapters.ServiceBus;

/// <summary>
/// Azure Service Bus implementation of IMessageBus with topics and subscriptions.
/// </summary>
public class ServiceBusMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly ServiceBusOptions _options;
    private readonly ICorrelationAccessor _correlationAccessor;
    private readonly ILogger<ServiceBusMessageBus> _logger;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ConcurrentDictionary<Type, ServiceBusProcessor> _processors = new();

    public ServiceBusMessageBus(
        IOptions<ServiceBusOptions> options,
        ICorrelationAccessor correlationAccessor,
        ILogger<ServiceBusMessageBus> logger)
    {
        _options = options.Value;
        _correlationAccessor = correlationAccessor;
        _logger = logger;

        _client = new ServiceBusClient(_options.ConnectionString);
        _sender = _client.CreateSender(_options.TopicName);
        _adminClient = new ServiceBusAdministrationClient(_options.ConnectionString);

        EnsureTopicExistsAsync().GetAwaiter().GetResult();

        _logger.LogInformation("Service Bus client initialized for topic {TopicName}", _options.TopicName);
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        var envelope = MessageEnvelope<T>.Create(message, _correlationAccessor);
        await PublishAsync(envelope, cancellationToken);
    }

    public async Task PublishAsync<T>(MessageEnvelope<T> envelope, CancellationToken cancellationToken = default)
        where T : class
    {
        var json = JsonSerializer.Serialize(envelope);
        var serviceBusMessage = new ServiceBusMessage(json)
        {
            MessageId = envelope.MessageId,
            CorrelationId = envelope.CorrelationId,
            Subject = typeof(T).Name,
            ContentType = "application/json"
        };

        // Add custom properties
        serviceBusMessage.ApplicationProperties["MessageType"] = typeof(T).Name;
        serviceBusMessage.ApplicationProperties["SchemaVersion"] = envelope.SchemaVersion;
        
        if (envelope.CausationId != null)
            serviceBusMessage.ApplicationProperties["CausationId"] = envelope.CausationId;

        try
        {
            await _sender.SendMessageAsync(serviceBusMessage, cancellationToken);
            
            _logger.LogDebug("Published message {MessageId} to topic {Topic}",
                envelope.MessageId, _options.TopicName);
        }
        catch (ServiceBusException ex)
        {
            _logger.LogError(ex, "Failed to publish message {MessageId} to Service Bus",
                envelope.MessageId);
            throw;
        }
    }

    public async Task SubscribeAsync<T>(
        Func<MessageEnvelope<T>, MessageContext, Task> handler,
        CancellationToken cancellationToken = default) where T : class
    {
        var subscriptionName = GetSubscriptionName<T>();

        // Ensure subscription exists
        await EnsureSubscriptionExistsAsync<T>(subscriptionName);

        var processor = _client.CreateProcessor(_options.TopicName, subscriptionName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 10,
            PrefetchCount = 10
        });

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(args.Message.Body);
                var envelope = JsonSerializer.Deserialize<MessageEnvelope<T>>(json);

                if (envelope != null)
                {
                    var context = MessageContext.FromEnvelope(envelope);
                    
                    try
                    {
                        await handler(envelope, context);
                        await args.CompleteMessageAsync(args.Message, cancellationToken);
                        
                        _logger.LogDebug("Processed message {MessageId}", envelope.MessageId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message {MessageId}", envelope.MessageId);
                        
                        // Check delivery count
                        if (args.Message.DeliveryCount >= _options.MaxDeliveryCount)
                        {
                            _logger.LogWarning("Message {MessageId} exceeded max delivery count, moving to DLQ",
                                envelope.MessageId);
                            await args.DeadLetterMessageAsync(args.Message, 
                                "MaxDeliveryCountExceeded", 
                                ex.Message,
                                cancellationToken);
                        }
                        else
                        {
                            // Abandon to retry
                            await args.AbandonMessageAsync(args.Message, cancellationToken: cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in message processor");
                await args.AbandonMessageAsync(args.Message, cancellationToken: cancellationToken);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "Service Bus processor error for {EntityPath}",
                args.EntityPath);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(cancellationToken);
        _processors[typeof(T)] = processor;

        _logger.LogInformation("Subscribed to Service Bus topic {Topic} with subscription {Subscription}",
            _options.TopicName, subscriptionName);
    }

    public async Task UnsubscribeAsync<T>(CancellationToken cancellationToken = default) where T : class
    {
        if (_processors.TryRemove(typeof(T), out var processor))
        {
            await processor.StopProcessingAsync(cancellationToken);
            await processor.DisposeAsync();
            
            _logger.LogInformation("Unsubscribed from subscription {Subscription}",
                GetSubscriptionName<T>());
        }
    }

    private async Task EnsureTopicExistsAsync()
    {
        try
        {
            if (!await _adminClient.TopicExistsAsync(_options.TopicName))
            {
                var topicOptions = new CreateTopicOptions(_options.TopicName)
                {
                    DefaultMessageTimeToLive = _options.MessageTimeToLive ?? TimeSpan.FromDays(14),
                    EnablePartitioning = true
                };

                await _adminClient.CreateTopicAsync(topicOptions);
                _logger.LogInformation("Created Service Bus topic {TopicName}", _options.TopicName);
            }
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // Topic already exists, ignore
        }
    }

    private async Task EnsureSubscriptionExistsAsync<T>(string subscriptionName)
    {
        try
        {
            if (!await _adminClient.SubscriptionExistsAsync(_options.TopicName, subscriptionName))
            {
                var subscriptionOptions = new CreateSubscriptionOptions(_options.TopicName, subscriptionName)
                {
                    DefaultMessageTimeToLive = _options.MessageTimeToLive ?? TimeSpan.FromDays(14),
                    MaxDeliveryCount = _options.MaxDeliveryCount
                };

                // Add filter for message type
                var ruleOptions = new CreateRuleOptions
                {
                    Name = $"{typeof(T).Name}Filter",
                    Filter = new CorrelationRuleFilter
                    {
                        Subject = typeof(T).Name
                    }
                };

                await _adminClient.CreateSubscriptionAsync(subscriptionOptions, ruleOptions);
                _logger.LogInformation("Created Service Bus subscription {SubscriptionName}", subscriptionName);
            }
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // Subscription already exists, ignore
        }
    }

    private string GetSubscriptionName<T>()
    {
        var baseSubscriptionName = !string.IsNullOrEmpty(_options.SubscriptionName)
            ? _options.SubscriptionName
            : $"{_options.ServiceName}-{typeof(T).Name}";
        
        return baseSubscriptionName.ToLowerInvariant();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var processor in _processors.Values)
        {
            await processor.StopProcessingAsync();
            await processor.DisposeAsync();
        }

        await _sender.DisposeAsync();
        await _client.DisposeAsync();
        
        _logger.LogInformation("Service Bus MessageBus disposed");
    }
}

/// <summary>
/// Configuration options for Azure Service Bus.
/// </summary>
public class ServiceBusOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string TopicName { get; set; } = "omniflow";
    public string SubscriptionName { get; set; } = "default-subscription";
    public int MaxDeliveryCount { get; set; } = 10;
    public TimeSpan? MessageTimeToLive { get; set; } = TimeSpan.FromDays(14);
    public string ServiceName { get; set; } = "default";
}
