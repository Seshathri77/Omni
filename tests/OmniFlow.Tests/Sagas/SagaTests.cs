using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniFlow.Core;
using OmniFlow.Messaging;
using OmniFlow.Sagas;
using Xunit;

namespace OmniFlow.Tests.Sagas;

public class SagaTests
{
    [Fact]
    public async Task Should_Start_Saga_And_Save_State()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var accessor = new CorrelationAccessor();
        var messageBus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        var saga = new TestSaga();
        saga.Initialize(repository, messageBus);

        // Act
        await saga.StartAsync("correlation-123");

        // Assert
        var state = await repository.GetAsync(saga.GetState().SagaId);
        state.Should().NotBeNull();
        state!.Value.State.Status.Should().Be(SagaStatus.Running);
        state.Value.State.CorrelationId.Should().Be("correlation-123");
    }

    [Fact]
    public async Task Should_Complete_Saga()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var accessor = new CorrelationAccessor();
        var messageBus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        var saga = new TestSaga();
        saga.Initialize(repository, messageBus);
        await saga.StartAsync("correlation-123");

        // Act
        await saga.CompleteTestAsync();

        // Assert
        var state = await repository.GetAsync(saga.GetState().SagaId);
        state!.Value.State.Status.Should().Be(SagaStatus.Completed);
    }

    [Fact]
    public async Task Should_Compensate_Saga()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var accessor = new CorrelationAccessor();
        var messageBus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        var saga = new TestSaga();
        saga.Initialize(repository, messageBus);
        await saga.StartAsync("correlation-123");

        // Act
        await saga.CompensateTestAsync("Test reason");

        // Assert
        var state = await repository.GetAsync(saga.GetState().SagaId);
        state!.Value.State.Status.Should().Be(SagaStatus.Compensated);
        state.Value.State.CompensationExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Track_History()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var accessor = new CorrelationAccessor();
        var messageBus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        var saga = new TestSaga();
        saga.Initialize(repository, messageBus);

        // Act
        await saga.StartAsync("correlation-123");
        await saga.CompleteTestAsync();

        // Assert
        var state = await repository.GetAsync(saga.GetState().SagaId);
        state!.Value.State.History.Should().NotBeEmpty();
        state.Value.State.History.Should().Contain(h => h.Contains("Saga started"));
        state.Value.State.History.Should().Contain(h => h.Contains("Saga completed"));
    }
}

public class TestSagaState : SagaState
{
    public bool CompensationExecuted { get; set; }
}

public class TestSaga : Saga<TestSagaState>
{
    public async Task CompleteTestAsync()
    {
        await CompleteAsync();
    }

    public async Task CompensateTestAsync(string reason)
    {
        await CompensateAsync(reason);
    }

    protected override Task OnCompensateAsync(CancellationToken cancellationToken)
    {
        State.CompensationExecuted = true;
        return Task.CompletedTask;
    }

    public TestSagaState GetState() => State;
}
