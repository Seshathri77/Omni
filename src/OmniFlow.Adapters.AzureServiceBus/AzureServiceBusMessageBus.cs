using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniFlow.Core;
using OmniFlow.Messaging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace OmniFlow.Adapters.AzureServiceBus;

/// <summary>
/// Azure Service Bus implementation of IMessageBus.
/// </summary>
public class AzureServiceBusMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly ServiceBusOptions _options;
    private readonly ICorrelationAccessor _correlationAccessor;
    private readonly ILogger<AzureServiceBusMessageBus> _logger;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;
    private readonly ConcurrentDictionary<Type, object> _processors = new(); // Stores either ServiceBusProcessor or ServiceBusSessionProcessor

    public AzureServiceBusMessageBus(
        IOptions<ServiceBusOptions> options,
        ICorrelationAccessor correlationAccessor,
        ILogger<AzureServiceBusMessageBus> logger)
    {
        _options = options.Value;
        _correlationAccessor = correlationAccessor;
        _logger = logger;

        // Create Service Bus client
        if (!string.IsNullOrEmpty(_options.ConnectionString))
        {
            _client = new ServiceBusClient(_options.ConnectionString);
        }
        else if (!string.IsNullOrEmpty(_options.FullyQualifiedNamespace))
        {
            // For Managed Identity/DefaultAzureCredential
            var credential = new Azure.Identity.DefaultAzureCredential();
            _client = new ServiceBusClient(_options.FullyQualifiedNamespace, credential);
        }
        else
        {
            throw new InvalidOperationException(
                "Either ConnectionString or FullyQualifiedNamespace must be provided.");
        }

        // Create sender for the topic
        _sender = _client.CreateSender(_options.TopicName);
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        var envelope = MessageEnvelope<T>.Create(message, _correlationAccessor);
        return PublishAsync(envelope, cancellationToken);
    }

    public async Task PublishAsync<T>(MessageEnvelope<T> envelope, CancellationToken cancellationToken = default) 
        where T : class
    {
        var messageType = typeof(T).Name;
        var body = BinaryData.FromString(JsonSerializer.Serialize(envelope));

        var serviceBusMessage = new ServiceBusMessage(body)
        {
            MessageId = envelope.MessageId,
            CorrelationId = envelope.CorrelationId,
            Subject = messageType, // Used for subscription filtering
            ContentType = "application/json",
            SessionId = _options.EnableSessions ? envelope.CorrelationId : null
        };

        // Add custom application properties
        serviceBusMessage.ApplicationProperties["CausationId"] = envelope.CausationId;
        serviceBusMessage.ApplicationProperties["MessageType"] = typeof(T).FullName ?? messageType;
        serviceBusMessage.ApplicationProperties["SchemaVersion"] = envelope.SchemaVersion;
        serviceBusMessage.ApplicationProperties["Timestamp"] = envelope.Timestamp.ToString("O");

        try
        {
            await _sender.SendMessageAsync(serviceBusMessage, cancellationToken);
            
            _logger.LogDebug(
                "Published message {MessageId} to topic {Topic} with subject {Subject}",
                envelope.MessageId, _options.TopicName, messageType);
        }
        catch (ServiceBusException ex)
        {
            _logger.LogError(ex, 
                "Error publishing message {MessageId} to topic {Topic}", 
                envelope.MessageId, _options.TopicName);
            throw;
        }
    }

    public async Task SubscribeAsync<T>(
        Func<MessageEnvelope<T>, MessageContext, Task> handler,
        CancellationToken cancellationToken = default) where T : class
    {
        var messageType = typeof(T);
        
        if (_processors.ContainsKey(messageType))
        {
            _logger.LogWarning("Already subscribed to {MessageType}", messageType.Name);
            return;
        }

        var subscriptionName = GetSubscriptionName<T>();

        object processor;

        if (_options.EnableSessions)
        {
            var sessionProcessorOptions = new ServiceBusSessionProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentSessions = _options.MaxConcurrentCalls,
                PrefetchCount = _options.PrefetchCount,
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5),
                SessionIdleTimeout = TimeSpan.FromMinutes(1)
            };
            var sessionProcessor = _client.CreateSessionProcessor(_options.TopicName, subscriptionName, sessionProcessorOptions);
            
            sessionProcessor.ProcessMessageAsync += CreateSessionMessageHandler<T>(handler, subscriptionName);
            sessionProcessor.ProcessErrorAsync += CreateErrorHandler(subscriptionName);
            
            await sessionProcessor.StartProcessingAsync(cancellationToken);
            processor = sessionProcessor;
        }
        else
        {
            var processorOptions = new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = _options.MaxConcurrentCalls,
                PrefetchCount = _options.PrefetchCount,
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
            };
            var regularProcessor = _client.CreateProcessor(_options.TopicName, subscriptionName, processorOptions);
            
            regularProcessor.ProcessMessageAsync += CreateMessageHandler<T>(handler, subscriptionName);
            regularProcessor.ProcessErrorAsync += CreateErrorHandler(subscriptionName);
            
            await regularProcessor.StartProcessingAsync(cancellationToken);
            processor = regularProcessor;
        }

        _processors.TryAdd(messageType, processor);

        _logger.LogInformation(
            "Started processing subscription {Subscription} on topic {Topic}",
            subscriptionName, _options.TopicName);
    }

    private Func<ProcessSessionMessageEventArgs, Task> CreateSessionMessageHandler<T>(
        Func<MessageEnvelope<T>, MessageContext, Task> handler,
        string subscriptionName) where T : class
    {
        return async (args) =>
        {
            await ProcessMessageInternalAsync(args.Message, handler, subscriptionName, args);
        };
    }

    private Func<ProcessMessageEventArgs, Task> CreateMessageHandler<T>(
        Func<MessageEnvelope<T>, MessageContext, Task> handler,
        string subscriptionName) where T : class
    {
        return async (args) =>
        {
            await ProcessMessageInternalAsync(args.Message, handler, subscriptionName, args);
        };
    }

    private async Task ProcessMessageInternalAsync<T>(
        ServiceBusReceivedMessage message,
        Func<MessageEnvelope<T>, MessageContext, Task> handler,
        string subscriptionName,
        object args) where T : class
    {
        try
        {
            var body = message.Body.ToString();
            var envelope = JsonSerializer.Deserialize<MessageEnvelope<T>>(body);

            if (envelope != null)
            {
                var context = MessageContext.FromEnvelope(envelope);
                
                try
                {
                    await handler(envelope, context);
                    
                    // Complete the message after successful processing
                    var cancellationToken = args switch
                    {
                        ProcessMessageEventArgs messageArgs => messageArgs.CancellationToken,
                        ProcessSessionMessageEventArgs sessionArgs => sessionArgs.CancellationToken,
                        _ => CancellationToken.None
                    };

                    if (args is ProcessMessageEventArgs processArgs)
                    {
                        await processArgs.CompleteMessageAsync(message, cancellationToken);
                    }
                    else if (args is ProcessSessionMessageEventArgs sessionProcessArgs)
                    {
                        await sessionProcessArgs.CompleteMessageAsync(message, cancellationToken);
                    }
                    
                    _logger.LogDebug(
                        "Processed message {MessageId} from subscription {Subscription}",
                        envelope.MessageId, subscriptionName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Error processing message {MessageId} from subscription {Subscription}",
                        envelope.MessageId, subscriptionName);
                    
                    // Check if message has exceeded max delivery count
                    if (message.DeliveryCount >= _options.MaxDeliveryCount)
                    {
                        _logger.LogWarning(
                            "Message {MessageId} exceeded max delivery count ({MaxDeliveryCount}), moving to dead-letter queue",
                            envelope.MessageId, _options.MaxDeliveryCount);
                        
                        var cancellationToken = args switch
                        {
                            ProcessMessageEventArgs messageArgs => messageArgs.CancellationToken,
                            ProcessSessionMessageEventArgs sessionArgs => sessionArgs.CancellationToken,
                            _ => CancellationToken.None
                        };

                        if (args is ProcessMessageEventArgs processArgs)
                        {
                            await processArgs.DeadLetterMessageAsync(message, "ProcessingFailed", ex.Message, cancellationToken);
                        }
                        else if (args is ProcessSessionMessageEventArgs sessionProcessArgs)
                        {
                            await sessionProcessArgs.DeadLetterMessageAsync(message, "ProcessingFailed", ex.Message, cancellationToken);
                        }
                    }
                    else
                    {
                        // Abandon message for retry
                        var cancellationToken = args switch
                        {
                            ProcessMessageEventArgs messageArgs => messageArgs.CancellationToken,
                            ProcessSessionMessageEventArgs sessionArgs => sessionArgs.CancellationToken,
                            _ => CancellationToken.None
                        };

                        if (args is ProcessMessageEventArgs processArgs)
                        {
                            await processArgs.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                        }
                        else if (args is ProcessSessionMessageEventArgs sessionProcessArgs)
                        {
                            await sessionProcessArgs.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in message processor");
            throw;
        }
    }

    private Func<ProcessErrorEventArgs, Task> CreateErrorHandler(string subscriptionName)
    {
        return (args) =>
        {
            _logger.LogError(args.Exception, 
                "Error in Service Bus processor for subscription {Subscription}: {ErrorSource}",
                subscriptionName, args.ErrorSource);
            return Task.CompletedTask;
        };
    }

    public async Task UnsubscribeAsync<T>(CancellationToken cancellationToken = default) where T : class
    {
        var messageType = typeof(T);

        if (_processors.TryRemove(messageType, out var processor))
        {
            switch (processor)
            {
                case ServiceBusProcessor regularProcessor:
                    await regularProcessor.StopProcessingAsync(cancellationToken);
                    await regularProcessor.DisposeAsync();
                    break;
                case ServiceBusSessionProcessor sessionProcessor:
                    await sessionProcessor.StopProcessingAsync(cancellationToken);
                    await sessionProcessor.DisposeAsync();
                    break;
            }
            
            _logger.LogInformation("Unsubscribed from {MessageType}", messageType.Name);
        }
    }

    private string GetSubscriptionName<T>()
    {
        var messageTypeName = typeof(T).Name.ToLowerInvariant();
        return string.IsNullOrEmpty(_options.SubscriptionPrefix)
            ? $"{_options.ServiceName}-{messageTypeName}"
            : $"{_options.SubscriptionPrefix}-{_options.ServiceName}-{messageTypeName}";
    }

    public async ValueTask DisposeAsync()
    {
        // Stop and dispose all processors
        foreach (var processor in _processors.Values)
        {
            switch (processor)
            {
                case ServiceBusProcessor regularProcessor:
                    await regularProcessor.StopProcessingAsync();
                    await regularProcessor.DisposeAsync();
                    break;
                case ServiceBusSessionProcessor sessionProcessor:
                    await sessionProcessor.StopProcessingAsync();
                    await sessionProcessor.DisposeAsync();
                    break;
            }
        }
        _processors.Clear();

        // Dispose sender and client
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}

/// <summary>
/// Configuration options for Azure Service Bus.
/// </summary>
public class ServiceBusOptions
{
    /// <summary>
    /// Service Bus connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Fully qualified namespace (e.g., "myservicebus.servicebus.windows.net").
    /// Used with Managed Identity instead of connection string.
    /// </summary>
    public string? FullyQualifiedNamespace { get; set; }

    /// <summary>
    /// Topic name for publishing messages.
    /// </summary>
    public string TopicName { get; set; } = "omniflow";

    /// <summary>
    /// Service name used in subscription naming.
    /// </summary>
    public string ServiceName { get; set; } = "default";

    /// <summary>
    /// Optional subscription prefix (e.g., "prod" results in "prod-servicename-messagetype").
    /// </summary>
    public string? SubscriptionPrefix { get; set; }

    /// <summary>
    /// Enable sessions for ordered message processing per correlation ID.
    /// </summary>
    public bool EnableSessions { get; set; } = true;

    /// <summary>
    /// Maximum number of concurrent message processing calls.
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 10;

    /// <summary>
    /// Number of messages to prefetch.
    /// </summary>
    public int PrefetchCount { get; set; } = 0;

    /// <summary>
    /// Maximum delivery count before moving to dead-letter queue.
    /// </summary>
    public int MaxDeliveryCount { get; set; } = 10;
}
