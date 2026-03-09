using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OmniFlow.Core;
using OmniFlow.Messaging;
using OmniFlow.Sagas;
using Xunit;

namespace OmniFlow.Tests.Sagas;

public class SagaSubscriptionExtensionsTests
{
    [Fact]
    public async Task SubscribeSagaStart_Should_Pass_CancellationToken_To_Handler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        services.AddSingleton<ISagaRepository<TestSagaState>, InMemorySagaRepository<TestSagaState>>();
        services.AddSingleton<IMessageBus>(sp => 
            new InMemoryMessageBus(
                sp.GetRequiredService<ICorrelationAccessor>(),
                NullLogger<InMemoryMessageBus>.Instance));
        services.AddTransient<TestSaga>();
        
        var serviceProvider = services.BuildServiceProvider();
        var messageBus = serviceProvider.GetRequiredService<IMessageBus>();

        CancellationToken receivedToken = default;
        var cts = new CancellationTokenSource();
        
        // Act
        await messageBus.SubscribeSagaStart<TestSaga, TestSagaState, TestStartMessage>(
            serviceProvider,
            async (saga, message, ct) =>
            {
                receivedToken = ct;
                await saga.StartAsync(message.CorrelationId, ct);
            });

        // Publish with cancellation token
        await messageBus.PublishAsync(new TestStartMessage("test-correlation"), cts.Token);
        await Task.Delay(100); // Give time for async processing

        // Assert
        receivedToken.Should().NotBe(default(CancellationToken));
        receivedToken.CanBeCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task SubscribeSagaContinue_Should_Pass_CancellationToken_To_Handler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        services.AddSingleton<ISagaRepository<TestSagaState>, InMemorySagaRepository<TestSagaState>>();
        services.AddSingleton<IMessageBus>(sp => 
            new InMemoryMessageBus(
                sp.GetRequiredService<ICorrelationAccessor>(),
                NullLogger<InMemoryMessageBus>.Instance));
        services.AddTransient<TestSaga>();
        
        var serviceProvider = services.BuildServiceProvider();
        var messageBus = serviceProvider.GetRequiredService<IMessageBus>();
        var repository = serviceProvider.GetRequiredService<ISagaRepository<TestSagaState>>();

        // Create an existing saga
        var saga = new TestSaga();
        saga.Initialize(repository, messageBus);
        await saga.StartAsync("test-correlation");
        var sagaId = saga.GetState().SagaId;

        CancellationToken receivedToken = default;
        var cts = new CancellationTokenSource();
        
        // Act
        await messageBus.SubscribeSagaContinue<TestSaga, TestSagaState, TestContinueMessage>(
            serviceProvider,
            msg => msg.SagaId,
            async (saga, message, ct) =>
            {
                receivedToken = ct;
                await saga.CompleteTestAsync();
            });

        // Publish with cancellation token
        await messageBus.PublishAsync(new TestContinueMessage(sagaId), cts.Token);
        await Task.Delay(100); // Give time for async processing

        // Assert
        receivedToken.Should().NotBe(default(CancellationToken));
        receivedToken.CanBeCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task MessageContext_Should_Contain_CancellationToken()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var bus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        MessageContext? receivedContext = null;
        await bus.SubscribeAsync<TestStartMessage>((envelope, context) =>
        {
            receivedContext = context;
            return Task.CompletedTask;
        });

        var cts = new CancellationTokenSource();

        // Act
        await bus.PublishAsync(new TestStartMessage("test"), cts.Token);
        await Task.Delay(100);

        // Assert
        receivedContext.Should().NotBeNull();
        receivedContext!.CancellationToken.Should().NotBe(default(CancellationToken));
        receivedContext.CancellationToken.CanBeCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task Cancelled_Token_Should_Throw_OperationCanceledException()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var bus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        var taskCompletionSource = new TaskCompletionSource<bool>();
        var cts = new CancellationTokenSource();
        
        await bus.SubscribeAsync<TestStartMessage>(async (envelope, context) =>
        {
            try
            {
                // Simulate long-running operation that respects cancellation
                await Task.Delay(1000, context.CancellationToken);
                taskCompletionSource.SetResult(false);
            }
            catch (OperationCanceledException)
            {
                taskCompletionSource.SetResult(true);
            }
        });

        // Act
        var publishTask = bus.PublishAsync(new TestStartMessage("test"), cts.Token);
        cts.Cancel(); // Cancel immediately
        
        // Wait a bit for the handler to process
        var wasCancelled = await Task.WhenAny(taskCompletionSource.Task, Task.Delay(500)) == taskCompletionSource.Task
            ? await taskCompletionSource.Task
            : false;

        // Assert
        wasCancelled.Should().BeTrue("the cancellation token should have cancelled the operation");
    }

    private record TestStartMessage(string CorrelationId) : IMessage;
    private record TestContinueMessage(string SagaId) : IMessage;

    private class TestSagaState : SagaState
    {
        public bool CompensationExecuted { get; set; }
    }

    private class TestSaga : Saga<TestSagaState>
    {
        public async Task CompleteTestAsync()
        {
            await CompleteAsync();
        }

        protected override Task OnCompensateAsync(CancellationToken cancellationToken)
        {
            State.CompensationExecuted = true;
            return Task.CompletedTask;
        }

        public TestSagaState GetState() => State;
    }
}
