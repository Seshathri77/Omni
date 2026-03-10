using System.Diagnostics.Metrics;

namespace OmniFlow.Observability;

/// <summary>
/// Metrics for OmniFlow operations with comprehensive business and operational metrics.
/// </summary>
public class OmniFlowMetrics
{
    public const string MeterName = "OmniFlow";
    
    private readonly Meter _meter;
    
    // Message metrics
    private readonly Counter<long> _messagesPublished;
    private readonly Counter<long> _messagesProcessed;
    private readonly Counter<long> _messagesFailed;
    private readonly Histogram<double> _messageProcessingDuration;
    private readonly Counter<long> _messagesRetried;
    private readonly Counter<long> _circuitBreakerOpened;
    
    // Saga metrics
    private readonly Counter<long> _sagasStarted;
    private readonly Counter<long> _sagasCompleted;
    private readonly Counter<long> _sagasCompensated;
    private readonly Counter<long> _sagasFailed;
    private readonly Histogram<double> _sagaDuration;
    private readonly ObservableGauge<int> _activeSagas;
    private int _activeSagasCount;
    
    // Idempotency metrics
    private readonly Counter<long> _duplicateMessagesDetected;
    
    // Repository metrics
    private readonly Counter<long> _repositoryOperations;
    private readonly Counter<long> _repositoryErrors;
    private readonly Counter<long> _optimisticConcurrencyFailures;

    public OmniFlowMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        // Message metrics
        _messagesPublished = _meter.CreateCounter<long>(
            "omniflow.messages.published",
            description: "Total number of messages published");

        _messagesProcessed = _meter.CreateCounter<long>(
            "omniflow.messages.processed",
            description: "Total number of messages processed successfully");

        _messagesFailed = _meter.CreateCounter<long>(
            "omniflow.messages.failed",
            description: "Total number of messages that failed processing");

        _messageProcessingDuration = _meter.CreateHistogram<double>(
            "omniflow.messages.processing_duration",
            unit: "ms",
            description: "Duration of message processing");

        _messagesRetried = _meter.CreateCounter<long>(
            "omniflow.messages.retried",
            description: "Total number of message retries");

        _circuitBreakerOpened = _meter.CreateCounter<long>(
            "omniflow.circuit_breaker.opened",
            description: "Total number of times the circuit breaker opened");

        // Saga metrics
        _sagasStarted = _meter.CreateCounter<long>(
            "omniflow.sagas.started",
            description: "Total number of sagas started");

        _sagasCompleted = _meter.CreateCounter<long>(
            "omniflow.sagas.completed",
            description: "Total number of sagas completed successfully");

        _sagasCompensated = _meter.CreateCounter<long>(
            "omniflow.sagas.compensated",
            description: "Total number of sagas compensated");

        _sagasFailed = _meter.CreateCounter<long>(
            "omniflow.sagas.failed",
            description: "Total number of sagas that failed");

        _sagaDuration = _meter.CreateHistogram<double>(
            "omniflow.sagas.duration",
            unit: "ms",
            description: "Duration of saga execution from start to completion/compensation");

        _activeSagas = _meter.CreateObservableGauge<int>(
            "omniflow.sagas.active",
            observeValue: () => _activeSagasCount,
            description: "Number of currently active sagas");

        // Idempotency metrics
        _duplicateMessagesDetected = _meter.CreateCounter<long>(
            "omniflow.idempotency.duplicates_detected",
            description: "Total number of duplicate messages detected");

        // Repository metrics
        _repositoryOperations = _meter.CreateCounter<long>(
            "omniflow.repository.operations",
            description: "Total number of repository operations");

        _repositoryErrors = _meter.CreateCounter<long>(
            "omniflow.repository.errors",
            description: "Total number of repository errors");

        _optimisticConcurrencyFailures = _meter.CreateCounter<long>(
            "omniflow.repository.concurrency_failures",
            description: "Total number of optimistic concurrency failures");
    }

    // Message metrics
    public void RecordMessagePublished(string messageType) =>
        _messagesPublished.Add(1, new KeyValuePair<string, object?>("message_type", messageType));

    public void RecordMessageProcessed(string messageType) =>
        _messagesProcessed.Add(1, new KeyValuePair<string, object?>("message_type", messageType));

    public void RecordMessageFailed(string messageType, string errorType) =>
        _messagesFailed.Add(1,
            new KeyValuePair<string, object?>("message_type", messageType),
            new KeyValuePair<string, object?>("error_type", errorType));

    public void RecordProcessingDuration(string messageType, double durationMs) =>
        _messageProcessingDuration.Record(durationMs,
            new KeyValuePair<string, object?>("message_type", messageType));

    public void RecordMessageRetried(string messageType, int attemptNumber) =>
        _messagesRetried.Add(1,
            new KeyValuePair<string, object?>("message_type", messageType),
            new KeyValuePair<string, object?>("attempt", attemptNumber));

    public void RecordCircuitBreakerOpened(string messageType) =>
        _circuitBreakerOpened.Add(1,
            new KeyValuePair<string, object?>("message_type", messageType));

    // Saga metrics
    public void RecordSagaStarted(string sagaType)
    {
        _sagasStarted.Add(1, new KeyValuePair<string, object?>("saga_type", sagaType));
        Interlocked.Increment(ref _activeSagasCount);
    }

    public void RecordSagaCompleted(string sagaType, double durationMs)
    {
        _sagasCompleted.Add(1, new KeyValuePair<string, object?>("saga_type", sagaType));
        _sagaDuration.Record(durationMs, new KeyValuePair<string, object?>("saga_type", sagaType));
        Interlocked.Decrement(ref _activeSagasCount);
    }

    public void RecordSagaCompensated(string sagaType, double durationMs)
    {
        _sagasCompensated.Add(1, new KeyValuePair<string, object?>("saga_type", sagaType));
        _sagaDuration.Record(durationMs, new KeyValuePair<string, object?>("saga_type", sagaType));
        Interlocked.Decrement(ref _activeSagasCount);
    }

    public void RecordSagaFailed(string sagaType, string errorType, double durationMs)
    {
        _sagasFailed.Add(1,
            new KeyValuePair<string, object?>("saga_type", sagaType),
            new KeyValuePair<string, object?>("error_type", errorType));
        _sagaDuration.Record(durationMs, new KeyValuePair<string, object?>("saga_type", sagaType));
        Interlocked.Decrement(ref _activeSagasCount);
    }

    // Idempotency metrics
    public void RecordDuplicateMessageDetected(string messageType) =>
        _duplicateMessagesDetected.Add(1, new KeyValuePair<string, object?>("message_type", messageType));

    // Repository metrics
    public void RecordRepositoryOperation(string operation, string entityType) =>
        _repositoryOperations.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("entity_type", entityType));

    public void RecordRepositoryError(string operation, string entityType, string errorType) =>
        _repositoryErrors.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("entity_type", entityType),
            new KeyValuePair<string, object?>("error_type", errorType));

    public void RecordOptimisticConcurrencyFailure(string entityType) =>
        _optimisticConcurrencyFailures.Add(1,
            new KeyValuePair<string, object?>("entity_type", entityType));
}
