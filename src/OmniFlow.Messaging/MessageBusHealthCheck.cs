using Microsoft.Extensions.Diagnostics.HealthChecks;
using OmniFlow.Core;

namespace OmniFlow.Messaging;

/// <summary>
/// Health check for message bus connectivity and availability.
/// </summary>
public class MessageBusHealthCheck : IHealthCheck
{
    private readonly IMessageBus _messageBus;

    public MessageBusHealthCheck(IMessageBus messageBus)
    {
        _messageBus = messageBus;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if message bus is available by attempting a health check message
            // For in-memory bus, this is always healthy
            // For external buses (RabbitMQ, Kafka), they should implement IHealthCheckable
            
            if (_messageBus is IHealthCheckable healthCheckable)
            {
                var isHealthy = await healthCheckable.CheckHealthAsync(cancellationToken);
                
                if (isHealthy)
                {
                    return HealthCheckResult.Healthy("Message bus is available and responsive");
                }
                else
                {
                    return HealthCheckResult.Unhealthy("Message bus is not responsive");
                }
            }

            // Default for buses that don't implement health checks (e.g., InMemoryMessageBus)
            return HealthCheckResult.Healthy("Message bus is available (no health check implemented)");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Message bus health check failed",
                ex);
        }
    }
}

/// <summary>
/// Interface for message bus implementations that support health checking.
/// </summary>
public interface IHealthCheckable
{
    /// <summary>
    /// Checks if the message bus is healthy and responsive.
    /// </summary>
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}
