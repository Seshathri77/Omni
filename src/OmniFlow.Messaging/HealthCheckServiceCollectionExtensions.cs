using Microsoft.Extensions.DependencyInjection;

namespace OmniFlow.Messaging;

/// <summary>
/// Extension methods for registering OmniFlow health checks.
/// </summary>
public static class HealthCheckServiceCollectionExtensions
{
    /// <summary>
    /// Adds message bus health check to the service collection.
    /// </summary>
    public static IHealthChecksBuilder AddOmniFlowMessageBusHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "message_bus",
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck<MessageBusHealthCheck>(
            name,
            failureStatus,
            tags);
    }
}
