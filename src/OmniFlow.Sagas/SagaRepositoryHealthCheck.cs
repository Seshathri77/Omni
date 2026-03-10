using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OmniFlow.Sagas;

/// <summary>
/// Health check for saga repository connectivity and availability.
/// </summary>
public class SagaRepositoryHealthCheck : IHealthCheck
{
    private readonly ISagaRepositoryHealthCheckable _repository;

    public SagaRepositoryHealthCheck(ISagaRepositoryHealthCheckable repository)
    {
        _repository = repository;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _repository.CheckHealthAsync(cancellationToken);

            if (isHealthy)
            {
                return HealthCheckResult.Healthy("Saga repository is available and responsive");
            }
            else
            {
                return HealthCheckResult.Unhealthy("Saga repository is not responsive");
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Saga repository health check failed",
                ex);
        }
    }
}

/// <summary>
/// Interface for saga repositories that support health checking.
/// </summary>
public interface ISagaRepositoryHealthCheckable
{
    /// <summary>
    /// Checks if the saga repository is healthy and responsive.
    /// </summary>
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}
