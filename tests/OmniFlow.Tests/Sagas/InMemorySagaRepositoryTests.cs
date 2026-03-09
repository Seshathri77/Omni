using FluentAssertions;
using OmniFlow.Sagas;
using Xunit;

namespace OmniFlow.Tests.Sagas;

public class InMemorySagaRepositoryTests
{
    [Fact]
    public async Task Should_Save_And_Retrieve_Saga_State()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var state = new TestSagaState
        {
            SagaId = "saga-123",
            CorrelationId = "correlation-123",
            Status = SagaStatus.Running
        };

        // Act
        await repository.SaveAsync("saga-123", state, 1);
        var result = await repository.GetAsync("saga-123");

        // Assert
        result.Should().NotBeNull();
        result!.Value.State.SagaId.Should().Be("saga-123");
        result.Value.State.CorrelationId.Should().Be("correlation-123");
        result.Value.Version.Should().Be(1);
    }

    [Fact]
    public async Task Should_Return_Null_For_Non_Existent_Saga()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();

        // Act
        var result = await repository.GetAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Should_Update_Existing_Saga()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var state = new TestSagaState
        {
            SagaId = "saga-123",
            CorrelationId = "correlation-123",
            Status = SagaStatus.Running
        };

        await repository.SaveAsync("saga-123", state, 1);

        // Act
        state.Status = SagaStatus.Completed;
        await repository.SaveAsync("saga-123", state, 2);
        var result = await repository.GetAsync("saga-123");

        // Assert
        result.Should().NotBeNull();
        result!.Value.State.Status.Should().Be(SagaStatus.Completed);
        result.Value.Version.Should().Be(2);
    }

    [Fact]
    public async Task Should_Delete_Saga()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var state = new TestSagaState
        {
            SagaId = "saga-123",
            CorrelationId = "correlation-123",
            Status = SagaStatus.Running
        };

        await repository.SaveAsync("saga-123", state, 1);

        // Act
        await repository.DeleteAsync("saga-123");
        var result = await repository.GetAsync("saga-123");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Should_Find_Sagas_By_Correlation()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        
        var state1 = new TestSagaState
        {
            SagaId = "saga-1",
            CorrelationId = "correlation-123",
            Status = SagaStatus.Running
        };

        var state2 = new TestSagaState
        {
            SagaId = "saga-2",
            CorrelationId = "correlation-123",
            Status = SagaStatus.Running
        };

        var state3 = new TestSagaState
        {
            SagaId = "saga-3",
            CorrelationId = "correlation-456",
            Status = SagaStatus.Running
        };

        await repository.SaveAsync("saga-1", state1, 1);
        await repository.SaveAsync("saga-2", state2, 1);
        await repository.SaveAsync("saga-3", state3, 1);

        // Act
        var matches = await repository.FindByCorrelationAsync("CorrelationId", "correlation-123");
        var matchList = matches.ToList();

        // Assert
        matchList.Should().HaveCount(2);
        matchList.Should().Contain("saga-1");
        matchList.Should().Contain("saga-2");
        matchList.Should().NotContain("saga-3");
    }

    [Fact]
    public async Task Should_Return_Empty_When_No_Correlation_Matches()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        
        var state = new TestSagaState
        {
            SagaId = "saga-1",
            CorrelationId = "correlation-123",
            Status = SagaStatus.Running
        };

        await repository.SaveAsync("saga-1", state, 1);

        // Act
        var matches = await repository.FindByCorrelationAsync("CorrelationId", "non-existent");
        var matchList = matches.ToList();

        // Assert
        matchList.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_Should_Not_Throw_For_Non_Existent_Saga()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();

        // Act
        var act = async () => await repository.DeleteAsync("non-existent");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Should_Handle_Cancellation_Token()
    {
        // Arrange
        var repository = new InMemorySagaRepository<TestSagaState>();
        var state = new TestSagaState
        {
            SagaId = "saga-123",
            CorrelationId = "correlation-123",
            Status = SagaStatus.Running
        };
        var cts = new CancellationTokenSource();

        // Act
        await repository.SaveAsync("saga-123", state, 1, cts.Token);
        var result = await repository.GetAsync("saga-123", cts.Token);
        await repository.DeleteAsync("saga-123", cts.Token);
        var matches = await repository.FindByCorrelationAsync("CorrelationId", "correlation-123", cts.Token);

        // Assert - operations complete successfully
        result.Should().NotBeNull();
        matches.Should().BeEmpty();
    }

    private class TestSagaState : SagaState
    {
        public string? CustomProperty { get; set; }
    }
}
