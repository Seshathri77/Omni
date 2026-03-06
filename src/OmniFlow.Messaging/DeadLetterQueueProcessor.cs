using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniFlow.Core;

namespace OmniFlow.Messaging;

/// <summary>
/// Background service that processes dead-letter queue messages.
/// </summary>
public class DeadLetterQueueProcessor : BackgroundService
{
    private readonly IDeadLetterQueueStore _dlqStore;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<DeadLetterQueueProcessor> _logger;
    private readonly DeadLetterQueueOptions _options;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(30);

    public DeadLetterQueueProcessor(
        IDeadLetterQueueStore dlqStore,
        IMessageBus messageBus,
        IOptions<DeadLetterQueueOptions> options,
        ILogger<DeadLetterQueueProcessor> logger)
    {
        _dlqStore = dlqStore;
        _messageBus = messageBus;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeadLetterQueueProcessor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRetryableMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DLQ processing loop");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessRetryableMessagesAsync(CancellationToken cancellationToken)
    {
        var messages = await _dlqStore.GetRetryableMessagesAsync(_options.BatchSize, cancellationToken);

        foreach (var dlqMessage in messages)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                if (ShouldRetry(dlqMessage))
                {
                    await RetryMessageAsync(dlqMessage, cancellationToken);
                }
                else
                {
                    await HandleExhaustedRetriesAsync(dlqMessage, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error processing DLQ message {MessageId}", 
                    dlqMessage.DeadLetterMessageId);
            }
        }
    }

    private bool ShouldRetry(DeadLetterMessage dlqMessage)
    {
        // Check if max retries exceeded
        if (dlqMessage.Metadata.RetryCount >= _options.MaxRetries)
        {
            return false;
        }

        // Check if it's time to retry (exponential backoff)
        if (dlqMessage.Metadata.NextRetryAt.HasValue &&
            dlqMessage.Metadata.NextRetryAt.Value > DateTimeOffset.UtcNow)
        {
            return false;
        }

        return true;
    }

    private async Task RetryMessageAsync(DeadLetterMessage dlqMessage, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Retrying DLQ message {MessageId} (attempt {RetryCount})",
                dlqMessage.MessageId,
                dlqMessage.Metadata.RetryCount + 1);

            // Deserialize and republish the original message
            var messageType = Type.GetType(dlqMessage.Metadata.OriginalMessageType);
            if (messageType == null)
            {
                _logger.LogError(
                    "Cannot find message type {MessageType} for DLQ message {MessageId}",
                    dlqMessage.Metadata.OriginalMessageType,
                    dlqMessage.MessageId);
                return;
            }

            var message = System.Text.Json.JsonSerializer.Deserialize(
                dlqMessage.MessageBody,
                messageType);

            if (message == null)
            {
                _logger.LogError(
                    "Failed to deserialize DLQ message {MessageId}",
                    dlqMessage.MessageId);
                return;
            }

            // Republish the message
            await _messageBus.PublishAsync(message, cancellationToken);

            // Remove from DLQ after successful retry
            await _dlqStore.RemoveAsync(dlqMessage.DeadLetterMessageId, cancellationToken);

            _logger.LogInformation(
                "Successfully retried DLQ message {MessageId}",
                dlqMessage.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retry DLQ message {MessageId}",
                dlqMessage.MessageId);

            // Update retry metadata
            var delay = CalculateRetryDelay(dlqMessage.Metadata.RetryCount + 1);
            var updatedMetadata = dlqMessage.Metadata with
            {
                RetryCount = dlqMessage.Metadata.RetryCount + 1,
                LastFailedAt = DateTimeOffset.UtcNow,
                NextRetryAt = DateTimeOffset.UtcNow.Add(delay),
                FailureReasons = dlqMessage.Metadata.FailureReasons
                    .Append($"Retry {dlqMessage.Metadata.RetryCount + 1}: {ex.Message}")
                    .ToArray()
            };

            await _dlqStore.UpdateRetryMetadataAsync(
                dlqMessage.DeadLetterMessageId,
                updatedMetadata,
                cancellationToken);
        }
    }

    private async Task HandleExhaustedRetriesAsync(
        DeadLetterMessage dlqMessage,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "DLQ message {MessageId} exhausted all {MaxRetries} retries. " +
            "Correlation: {CorrelationId}, OriginalQueue: {OriginalQueue}",
            dlqMessage.MessageId,
            _options.MaxRetries,
            dlqMessage.CorrelationId,
            dlqMessage.Metadata.OriginalQueue);

        // Publish alert event
        var alert = new DeadLetterMessageExhaustionAlert(
            dlqMessage.MessageId,
            dlqMessage.CorrelationId,
            dlqMessage.Metadata.OriginalQueue,
            dlqMessage.Metadata.OriginalMessageType,
            dlqMessage.Metadata.RetryCount,
            dlqMessage.Metadata.FailureReasons);

        await _messageBus.PublishAsync(alert, cancellationToken);

        // Optionally call webhook
        if (!string.IsNullOrEmpty(_options.AlertWebhook))
        {
            await SendAlertWebhookAsync(alert, cancellationToken);
        }
    }

    private TimeSpan CalculateRetryDelay(int retryCount)
    {
        // Exponential backoff: 1min, 5min, 15min, 30min, 1hour, ...
        var baseDelay = _options.InitialRetryDelay;
        var delay = TimeSpan.FromMilliseconds(
            baseDelay.TotalMilliseconds * Math.Pow(2, retryCount - 1));

        // Cap at max delay
        return delay > _options.MaxRetryDelay ? _options.MaxRetryDelay : delay;
    }

    private async Task SendAlertWebhookAsync(
        DeadLetterMessageExhaustionAlert alert,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var json = System.Text.Json.JsonSerializer.Serialize(alert);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(_options.AlertWebhook, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Sent DLQ alert webhook for message {MessageId}", alert.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send DLQ alert webhook");
        }
    }
}

/// <summary>
/// Configuration options for dead-letter queue processing.
/// </summary>
public class DeadLetterQueueOptions
{
    /// <summary>
    /// Maximum number of retry attempts before giving up (default: 3).
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial retry delay (default: 1 minute).
    /// </summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum retry delay (default: 1 hour).
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Batch size for processing DLQ messages (default: 100).
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Optional webhook URL to call when messages exhaust retries.
    /// </summary>
    public string? AlertWebhook { get; set; }
}

/// <summary>
/// Event published when a DLQ message exhausts all retries.
/// </summary>
public record DeadLetterMessageExhaustionAlert(
    string MessageId,
    string CorrelationId,
    string OriginalQueue,
    string MessageType,
    int RetryCount,
    string[] FailureReasons) : IEvent;
