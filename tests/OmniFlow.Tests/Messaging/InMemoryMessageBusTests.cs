using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniFlow.Core;
using OmniFlow.Messaging;
using Xunit;

namespace OmniFlow.Tests.Messaging;

public class InMemoryMessageBusTests
{
    [Fact]
    public async Task Should_Publish_And_Subscribe_To_Message()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var bus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        TestMessage? receivedMessage = null;
        await bus.SubscribeAsync<TestMessage>((envelope, context) =>
        {
            receivedMessage = envelope.Message;
            return Task.CompletedTask;
        });

        var message = new TestMessage("Hello");

        // Act
        await bus.PublishAsync(message);
        await Task.Delay(100); // Give time for async processing

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.Value.Should().Be("Hello");
    }

    [Fact]
    public async Task Should_Execute_Middleware_Pipeline()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var bus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        var middlewareExecuted = false;
        bus.UseMiddleware(new TestMiddleware(() => middlewareExecuted = true));

        await bus.SubscribeAsync<TestMessage>((envelope, context) => Task.CompletedTask);

        // Act
        await bus.PublishAsync(new TestMessage("Test"));
        await Task.Delay(100);

        // Assert
        middlewareExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Unsubscribe_From_Messages()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var bus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        var messageReceived = false;
        await bus.SubscribeAsync<TestMessage>((envelope, context) =>
        {
            messageReceived = true;
            return Task.CompletedTask;
        });

        // Act - Unsubscribe
        await bus.UnsubscribeAsync<TestMessage>();
        await bus.PublishAsync(new TestMessage("Test"));
        await Task.Delay(100);

        // Assert
        messageReceived.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Not_Throw_When_Publishing_With_No_Subscribers()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var bus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);

        // Act
        var act = async () => await bus.PublishAsync(new TestMessage("Test"));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Should_Handle_Multiple_Subscribers()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var bus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        var handler1Executed = false;
        var handler2Executed = false;

        await bus.SubscribeAsync<TestMessage>((envelope, context) =>
        {
            handler1Executed = true;
            return Task.CompletedTask;
        });

        await bus.SubscribeAsync<TestMessage>((envelope, context) =>
        {
            handler2Executed = true;
            return Task.CompletedTask;
        });

        // Act
        await bus.PublishAsync(new TestMessage("Test"));
        await Task.Delay(100);

        // Assert
        handler1Executed.Should().BeTrue();
        handler2Executed.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Pass_Envelope_To_Handler()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        accessor.SetContext("test-correlation");
        var bus = new InMemoryMessageBus(accessor, NullLogger<InMemoryMessageBus>.Instance);
        
        MessageEnvelope<TestMessage>? receivedEnvelope = null;
        await bus.SubscribeAsync<TestMessage>((envelope, context) =>
        {
            receivedEnvelope = envelope;
            return Task.CompletedTask;
        });

        // Act
        var message = new TestMessage("TestValue");
        await bus.PublishAsync(message);
        await Task.Delay(100);

        // Assert
        receivedEnvelope.Should().NotBeNull();
        receivedEnvelope!.Message.Value.Should().Be("TestValue");
        receivedEnvelope.CorrelationId.Should().Be("test-correlation");
    }

    private record TestMessage(string Value);

    private class TestMiddleware : IMessageMiddleware
    {
        private readonly Action _onExecute;

        public TestMiddleware(Action onExecute)
        {
            _onExecute = onExecute;
        }

        public async Task InvokeAsync<T>(MessageEnvelope<T> envelope, MessageContext context, Func<Task> next, 
            CancellationToken cancellationToken = default) where T : class
        {
            _onExecute();
            await next();
        }
    }
}
