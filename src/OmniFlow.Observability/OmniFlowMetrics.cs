using System.Diagnostics.Metrics;

namespace OmniFlow.Observability;

/// <summary>
/// Metrics for OmniFlow operations.
/// </summary>
public class OmniFlowMetrics
{
    public const string MeterName = "OmniFlow";
    
    private readonly Meter _meter;
    private readonly Counter<long> _messagesPublished;
    private readonly Counter<long> _messagesProcessed;
    private readonly Counter<long> _messagesFailed;
    private readonly Histogram<double> _messageProcessingDuration;
    private readonly Counter<long> _sagasStarted;
    private readonly Counter<long> _sagasCompleted;
    private readonly Counter<long> _sagasCompensated;

    public OmniFlowMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

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

        _sagasStarted = _meter.CreateCounter<long>(
            "omniflow.sagas.started",
            description: "Total number of sagas started");

        _sagasCompleted = _meter.CreateCounter<long>(
            "omniflow.sagas.completed",
            description: "Total number of sagas completed successfully");

        _sagasCompensated = _meter.CreateCounter<long>(
            "omniflow.sagas.compensated",
            description: "Total number of sagas compensated");
    }

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

    public void RecordSagaStarted(string sagaType) =>
        _sagasStarted.Add(1, new KeyValuePair<string, object?>("saga_type", sagaType));

    public void RecordSagaCompleted(string sagaType) =>
        _sagasCompleted.Add(1, new KeyValuePair<string, object?>("saga_type", sagaType));

    public void RecordSagaCompensated(string sagaType) =>
        _sagasCompensated.Add(1, new KeyValuePair<string, object?>("saga_type", sagaType));
}
