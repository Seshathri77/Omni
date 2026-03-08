using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OmniFlow.Messaging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace OmniFlow.Sagas.Outbox;

/// <summary>
/// Background service that publishes messages from the outbox.
/// </summary>
public class OutboxPublisher : BackgroundService
{
    private readonly IOutboxStore _outboxStore;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<OutboxPublisher> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly int _maxRetries;
    private readonly ConcurrentDictionary<string, MethodInfo> _methodCache = new();

    public OutboxPublisher(
        IOutboxStore outboxStore,
        IMessageBus messageBus,
        ILogger<OutboxPublisher> logger,
        TimeSpan? pollInterval = null,
        int maxRetries = 3)
    {
        _outboxStore = outboxStore;
        _messageBus = messageBus;
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
        _maxRetries = maxRetries;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Publisher started (polling every {Interval}s, max retries: {MaxRetries})", 
            _pollInterval.TotalSeconds, _maxRetries);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingMessagesAsync(stoppingToken);
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in outbox publisher main loop");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("Outbox Publisher stopped");
    }

    /// <summary>
    /// Publishes all pending messages from the outbox.
    /// Can be overridden for custom publishing logic.
    /// </summary>
    protected virtual async Task PublishPendingMessagesAsync(CancellationToken cancellationToken)
    {
        var messages = await _outboxStore.GetUnpublishedAsync(batchSize: 100, cancellationToken);
        var messageList = messages.ToList();

        if (messageList.Count > 0)
        {
            _logger.LogDebug("Publishing {Count} pending outbox messages", messageList.Count);
        }

        foreach (var message in messageList)
        {
            // Skip messages that have exceeded retry limit
            if (message.PublishAttempts >= _maxRetries)
            {
                _logger.LogWarning(
                    "Message {MessageId} has exceeded maximum retry attempts ({MaxRetries}). Moving to failed state.",
                    message.MessageId, _maxRetries);

                await OnMessageMaxRetriesExceededAsync(message, cancellationToken);
                continue;
            }

            await ProcessMessageAsync(message, cancellationToken);
        }

        // Cleanup old published messages (older than 7 days)
        await CleanupOldMessagesAsync(cancellationToken);
    }

    /// <summary>
    /// Processes a single outbox message.
    /// Can be overridden for custom message processing.
    /// </summary>
    protected virtual async Task ProcessMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            // Deserialize with proper type information
            var envelopeType = ResolveMessageType(message);

            if (envelopeType == null)
            {
                _logger.LogWarning(
                    "Could not resolve type '{TypeName}' for message {MessageId}. Incrementing retry count.",
                    message.MessageTypeName, message.MessageId);

                await UpdateFailedAttemptAsync(message, 
                    $"Type resolution failed: {message.MessageTypeName}", cancellationToken);
                return;
            }

            var envelope = DeserializeMessage(message, envelopeType);

            if (envelope == null)
            {
                _logger.LogWarning(
                    "Failed to deserialize message {MessageId} of type {TypeName}",
                    message.MessageId, message.MessageTypeName);

                await UpdateFailedAttemptAsync(message, "Deserialization failed", cancellationToken);
                return;
            }

            // Publish the message
            await PublishMessageAsync(envelope, envelopeType, cancellationToken);

            // Mark as successfully published
            await _outboxStore.MarkAsPublishedAsync(message.MessageId, cancellationToken);

            await OnMessagePublishedAsync(message, cancellationToken);

            _logger.LogDebug("Published outbox message {MessageId} of type {MessageType}", 
                message.MessageId, message.MessageType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to publish outbox message {MessageId} (attempt {Attempts}/{MaxRetries})", 
                message.MessageId, message.PublishAttempts + 1, _maxRetries);

            await UpdateFailedAttemptAsync(message, ex.Message, cancellationToken);
            await OnMessageFailedAsync(message, ex, cancellationToken);
        }
    }

    /// <summary>
    /// Resolves the message envelope type from the type name.
    /// </summary>
    protected virtual Type? ResolveMessageType(OutboxMessage message)
    {
        if (string.IsNullOrEmpty(message.MessageTypeName))
        {
            return null;
        }

        try
        {
            // Try to get from cache first
            if (_methodCache.ContainsKey(message.MessageTypeName))
            {
                return Type.GetType(message.MessageTypeName);
            }

            var type = Type.GetType(message.MessageTypeName);

            if (type == null)
            {
                // Try loading the assembly if type not found
                var assemblyName = message.MessageTypeName.Split(',').LastOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(assemblyName))
                {
                    try
                    {
                        Assembly.Load(assemblyName);
                        type = Type.GetType(message.MessageTypeName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load assembly {AssemblyName}", assemblyName);
                    }
                }
            }

            return type;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving type {TypeName}", message.MessageTypeName);
            return null;
        }
    }

    /// <summary>
    /// Deserializes the message JSON into the envelope object.
    /// </summary>
    protected virtual object? DeserializeMessage(OutboxMessage message, Type envelopeType)
    {
        try
        {
            return JsonSerializer.Deserialize(message.MessageJson, envelopeType);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization failed for message {MessageId}", message.MessageId);
            return null;
        }
    }

    /// <summary>
    /// Publishes the envelope to the message bus using reflection.
    /// </summary>
    protected virtual async Task PublishMessageAsync(object envelope, Type envelopeType, CancellationToken cancellationToken)
    {
        // Get the inner message type (T from MessageEnvelope<T>)
        var genericArgs = envelopeType.GetGenericArguments();
        if (genericArgs.Length == 0)
        {
            throw new InvalidOperationException($"Type {envelopeType.Name} is not a generic type");
        }

        // Get or cache the PublishAsync method
        var methodKey = envelopeType.FullName ?? envelopeType.Name;

        if (!_methodCache.TryGetValue(methodKey, out var publishMethod))
        {
            publishMethod = _messageBus.GetType()
                .GetMethod("PublishAsync", new[] { envelopeType, typeof(CancellationToken) });

            if (publishMethod != null)
            {
                _methodCache.TryAdd(methodKey, publishMethod);
            }
        }

        if (publishMethod == null)
        {
            throw new InvalidOperationException(
                $"Could not find PublishAsync method for type {envelopeType.Name}");
        }

        var task = publishMethod.Invoke(_messageBus, new[] { envelope, cancellationToken });

        if (task is Task asyncTask)
        {
            await asyncTask;
        }
        else
        {
            throw new InvalidOperationException("PublishAsync did not return a Task");
        }
    }

    /// <summary>
    /// Updates the outbox message with failed attempt information.
    /// This persists the retry count and error message back to the store.
    /// </summary>
    protected virtual async Task UpdateFailedAttemptAsync(
        OutboxMessage message, 
        string errorMessage, 
        CancellationToken cancellationToken)
    {
        message.PublishAttempts++;
        message.LastError = errorMessage;

        // Re-save the message with updated attempt information
        await _outboxStore.SaveAsync(message, cancellationToken);
    }

    /// <summary>
    /// Cleans up old published messages from the outbox.
    /// </summary>
    protected virtual async Task CleanupOldMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var retentionPeriod = TimeSpan.FromDays(7);
            await _outboxStore.DeleteOldMessagesAsync(retentionPeriod, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old outbox messages");
        }
    }

    /// <summary>
    /// Called when a message is successfully published.
    /// Can be overridden to add custom logic (e.g., metrics, notifications).
    /// </summary>
    protected virtual Task OnMessagePublishedAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        // Hook for derived classes
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a message fails to publish.
    /// Can be overridden to add custom logic (e.g., alerting, logging).
    /// </summary>
    protected virtual Task OnMessageFailedAsync(OutboxMessage message, Exception exception, CancellationToken cancellationToken)
    {
        // Hook for derived classes
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a message has exceeded the maximum retry attempts.
    /// Can be overridden to move messages to a dead letter queue or alert operators.
    /// </summary>
    protected virtual async Task OnMessageMaxRetriesExceededAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        // Default: Mark as published to remove from processing queue
        // In production, you might want to move to a DLQ instead
        await _outboxStore.MarkAsPublishedAsync(message.MessageId, cancellationToken);

        _logger.LogError(
            "Message {MessageId} permanently failed after {MaxRetries} attempts. Last error: {Error}",
            message.MessageId, _maxRetries, message.LastError);
    }
}
