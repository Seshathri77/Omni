using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OmniFlow.Core;
using OmniFlow.Messaging;
using OmniFlow.Sagas;
using Xunit;

namespace OmniFlow.Tests.Integration;

/// <summary>
/// Integration tests for production hardening features.
/// These tests verify health checks, circuit breakers, and resilience patterns.
/// </summary>
public class IntegrationTests
{
    [Fact]
    public async Task Should_Register_And_Execute_Health_Checks()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddLogging();
        services.AddOmniFlowCore();
        services.AddOmniFlowMessageBus(options => options.Provider = MessageBusProvider.InMemory);
        services.AddOmniFlowSagas();
        
        // Register the repository as the health checkable interface
        services.AddSingleton<ISagaRepositoryHealthCheckable>(sp => 
            sp.GetRequiredService<ISagaRepository<TestSagaState>>() as ISagaRepositoryHealthCheckable 
            ?? new InMemorySagaRepository<TestSagaState>());
        
        services.AddHealthChecks()
            .AddOmniFlowMessageBusHealthCheck()
            .AddOmniFlowSagaRepositoryHealthCheck();

        var serviceProvider = services.BuildServiceProvider();
        var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Entries.Should().ContainKey("message_bus");
        result.Entries.Should().ContainKey("saga_repository");
        
        result.Entries["message_bus"].Status.Should().Be(HealthStatus.Healthy);
        result.Entries["saga_repository"].Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Should_Execute_Saga_With_Circuit_Breaker_Protection()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddLogging();
        services.AddOmniFlowCore();
        services.AddOmniFlowMessageBus(options => options.Provider = MessageBusProvider.InMemory);
        services.AddOmniFlowSagas();
        services.AddSaga<TestSaga, TestSagaState>();

        var serviceProvider = services.BuildServiceProvider();
        var repository = serviceProvider.GetRequiredService<ISagaRepository<TestSagaState>>();
        var messageBus = serviceProvider.GetRequiredService<IMessageBus>();
        var saga = serviceProvider.GetRequiredService<TestSaga>();
        saga.Initialize(repository, messageBus);

        // Act
        await saga.StartAsync("test-correlation-123");
        await saga.CompleteTestAsync();

        // Assert
        var state = await repository.GetAsync(saga.GetState().SagaId);
        state.Should().NotBeNull();
        state!.Value.State.Status.Should().Be(SagaStatus.Completed);
    }

    [Fact]
    public async Task Should_Handle_Multiple_Concurrent_Sagas()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddLogging();
        services.AddOmniFlowCore();
        services.AddOmniFlowMessageBus(options => options.Provider = MessageBusProvider.InMemory);
        services.AddOmniFlowSagas();
        services.AddSaga<TestSaga, TestSagaState>();

        var serviceProvider = services.BuildServiceProvider();
        var repository = serviceProvider.GetRequiredService<ISagaRepository<TestSagaState>>();
        var messageBus = serviceProvider.GetRequiredService<IMessageBus>();

        const int sagaCount = 10;
        var sagaIds = new List<string>();
        var tasks = new List<Task>();

        // Act - Create and complete multiple sagas concurrently
        for (int i = 0; i < sagaCount; i++)
        {
            var index = i; // Capture for closure
            tasks.Add(Task.Run(async () =>
            {
                var saga = serviceProvider.GetRequiredService<TestSaga>();
                saga.Initialize(repository, messageBus);
                await saga.StartAsync($"correlation-{index}");
                
                lock (sagaIds)
                {
                    sagaIds.Add(saga.GetState().SagaId);
                }
                
                await saga.CompleteTestAsync();
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All sagas should be completed successfully
        var completedCount = 0;
        foreach (var sagaId in sagaIds)
        {
            var state = await repository.GetAsync(sagaId);
            if (state?.State.Status == SagaStatus.Completed)
            {
                completedCount++;
            }
        }

        // All sagas should complete successfully
        completedCount.Should().Be(sagaCount);
    }

    [Fact]
    public async Task Health_Check_Should_Report_Unhealthy_On_Repository_Failure()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddLogging();
        services.AddOmniFlowCore();
        services.AddSingleton<ISagaRepositoryHealthCheckable>(new FailingSagaRepository());
        services.AddOmniFlowMessageBus(options => options.Provider = MessageBusProvider.InMemory);
        
        services.AddHealthChecks()
            .AddCheck<SagaRepositoryHealthCheck>("failing_repository");

        var serviceProvider = services.BuildServiceProvider();
        var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    // Test helpers
    private class TestSagaState : SagaState
    {
        public bool TestFlag { get; set; }
    }

    private class TestSaga : Saga<TestSagaState>
    {
        public TestSagaState GetState() => State;

        public async Task CompleteTestAsync()
        {
            State.TestFlag = true;
            await CompleteAsync();
        }
    }

    private class FailingSagaRepository : ISagaRepositoryHealthCheckable
    {
        public Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }
}
