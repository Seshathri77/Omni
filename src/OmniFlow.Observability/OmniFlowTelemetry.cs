using OmniFlow.Core;
using System.Diagnostics;

namespace OmniFlow.Observability;

/// <summary>
/// Constants for OmniFlow telemetry.
/// </summary>
public static class OmniFlowTelemetry
{
    /// <summary>
    /// Activity source name for OmniFlow tracing.
    /// </summary>
    public const string ActivitySourceName = "OmniFlow";

    /// <summary>
    /// Activity source for creating spans.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// Creates a new activity for message processing.
    /// </summary>
    public static Activity? StartMessageActivity<T>(MessageEnvelope<T> envelope) where T : class
    {
        var activity = ActivitySource.StartActivity(
            $"Process {envelope.MessageType}",
            ActivityKind.Consumer);

        if (activity != null)
        {
            activity.SetTag("messaging.message_id", envelope.MessageId);
            activity.SetTag("messaging.correlation_id", envelope.CorrelationId);
            activity.SetTag("messaging.message_type", envelope.MessageType);
            activity.SetTag("messaging.schema_version", envelope.SchemaVersion);

            if (!string.IsNullOrEmpty(envelope.CausationId))
            {
                activity.SetTag("messaging.causation_id", envelope.CausationId);
            }
        }

        return activity;
    }

    /// <summary>
    /// Creates a new activity for saga execution.
    /// </summary>
    public static Activity? StartSagaActivity(string sagaName, string sagaId, string operation)
    {
        var activity = ActivitySource.StartActivity(
            $"Saga {sagaName}.{operation}",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("saga.name", sagaName);
            activity.SetTag("saga.id", sagaId);
            activity.SetTag("saga.operation", operation);
        }

        return activity;
    }
}
