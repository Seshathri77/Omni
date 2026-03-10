using Microsoft.Extensions.DependencyInjection;

namespace OmniFlow.Sagas;

/// <summary>
/// Extension methods for registering OmniFlow saga health checks.
/// </summary>
public static class HealthCheckServiceCollectionExtensions
{
    /// <summary>
    /// Adds saga repository health check to the service collection.
    /// </summary>
    public static IHealthChecksBuilder AddOmniFlowSagaRepositoryHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "saga_repository",
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck<SagaRepositoryHealthCheck>(
            name,
            failureStatus,
            tags);
    }
}
