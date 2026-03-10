using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using OmniFlow.Core;
using OmniFlow.Messaging;
using OmniFlow.Sagas;
using Xunit;

namespace OmniFlow.Tests.HealthChecks;

public class HealthCheckTests
{
    [Fact]
    public async Task MessageBus_HealthCheck_Should_Return_Healthy_For_InMemory()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var messageBus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        var healthCheck = new MessageBusHealthCheck(messageBus);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("available");
    }

    [Fact]
    public async Task SagaRepository_HealthCheck_Should_Return_Healthy_For_InMemory()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var healthCheckableRepository = repository as ISagaRepositoryHealthCheckable;
        var healthCheck = new SagaRepositoryHealthCheck(healthCheckableRepository!);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("available");
    }

    [Fact]
    public async Task SagaRepository_HealthCheck_Should_Return_Unhealthy_When_Repository_Fails()
    {
        // Arrange
        var failingRepository = new FailingSagaRepository();
        var healthCheck = new SagaRepositoryHealthCheck(failingRepository);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().NotBeNull();
    }

    private class TestSagaState : SagaState
    {
    }

    private class FailingSagaRepository : ISagaRepositoryHealthCheckable
    {
        public Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Repository is down");
        }
    }
}
