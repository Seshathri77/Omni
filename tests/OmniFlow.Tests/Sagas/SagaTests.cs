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

    [Fact]
    public async Task Should_Load_Existing_Saga()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var accessor = new CorrelationAccessor();
        var messageBus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        var saga1 = new TestSaga();
        saga1.Initialize(repository, messageBus);
        await saga1.StartAsync("correlation-123");
        var sagaId = saga1.GetState().SagaId;

        var saga2 = new TestSaga();
        saga2.Initialize(repository, messageBus);

        // Act
        var loaded = await saga2.LoadAsync(sagaId);

        // Assert
        loaded.Should().BeTrue();
        saga2.GetState().SagaId.Should().Be(sagaId);
        saga2.GetState().CorrelationId.Should().Be("correlation-123");
        saga2.GetState().Status.Should().Be(SagaStatus.Running);
    }

    [Fact]
    public async Task Should_Return_False_When_Loading_Non_Existent_Saga()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var accessor = new CorrelationAccessor();
        var messageBus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        var saga = new TestSaga();
        saga.Initialize(repository, messageBus);

        // Act
        var loaded = await saga.LoadAsync("non-existent-id");

        // Assert
        loaded.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Preserve_State_Across_Load()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var accessor = new CorrelationAccessor();
        var messageBus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        var saga1 = new TestSaga();
        saga1.Initialize(repository, messageBus);
        await saga1.StartAsync("correlation-123");
        saga1.GetState().CustomProperty = "test-value";
        // The state is saved automatically when saga operations are performed
        await saga1.CompleteTestAsync();
        var sagaId = saga1.GetState().SagaId;

        var saga2 = new TestSaga();
        saga2.Initialize(repository, messageBus);

        // Act
        await saga2.LoadAsync(sagaId);

        // Assert
        saga2.GetState().CustomProperty.Should().Be("test-value");
    }

    [Fact]
    public async Task Should_Handle_CancellationToken_In_Start()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var accessor = new CorrelationAccessor();
        var messageBus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        var saga = new TestSaga();
        saga.Initialize(repository, messageBus);
        var cts = new CancellationTokenSource();

        // Act
        await saga.StartAsync("correlation-123", cts.Token);

        // Assert
        var state = await repository.GetAsync(saga.GetState().SagaId);
        state.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_Handle_CancellationToken_In_Load()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var accessor = new CorrelationAccessor();
        var messageBus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        var saga1 = new TestSaga();
        saga1.Initialize(repository, messageBus);
        await saga1.StartAsync("correlation-123");
        var sagaId = saga1.GetState().SagaId;

        var saga2 = new TestSaga();
        saga2.Initialize(repository, messageBus);
        var cts = new CancellationTokenSource();

        // Act
        var loaded = await saga2.LoadAsync(sagaId, cts.Token);

        // Assert
        loaded.Should().BeTrue();
    }

    [Fact]
    public async Task Compensate_Should_Set_Status_To_Compensated()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var accessor = new CorrelationAccessor();
        var messageBus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        var saga = new TestSaga();
        saga.Initialize(repository, messageBus);
        await saga.StartAsync("correlation-123");

        // Act
        await saga.CompensateTestAsync("Compensation reason");

        // Assert
        saga.GetState().Status.Should().Be(SagaStatus.Compensated);
        saga.GetState().History.Should().Contain(h => h.Contains("compensation") || h.Contains("Compensation"));
    }
}

public class TestSagaState : SagaState
{
    public bool CompensationExecuted { get; set; }
    public string? CustomProperty { get; set; }
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
