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
