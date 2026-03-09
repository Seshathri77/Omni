using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniFlow.Core;
using OmniFlow.Messaging.Middleware;
using Xunit;

namespace OmniFlow.Tests.Messaging.Middleware;

public class CorrelationMiddlewareTests
{
    [Fact]
    public async Task Should_Set_Correlation_Context()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var middleware = new CorrelationMiddleware(accessor, NullLogger<CorrelationMiddleware>.Instance);
        
        var message = new TestMessage("test");
        var envelope = new MessageEnvelope<TestMessage>
        {
            Message = message,
            CorrelationId = "new-correlation-id",
            CausationId = "causation-123"
        };
        
        var context = MessageContext.FromEnvelope(envelope);
        var nextCalled = false;
        string? capturedCorrelationId = null;
        string? capturedCausationId = null;

        // Act
        await middleware.InvokeAsync(envelope, context, () =>
        {
            nextCalled = true;
            capturedCorrelationId = accessor.CorrelationId;
            capturedCausationId = accessor.CausationId;
            return Task.CompletedTask;
        });

        // Assert
        nextCalled.Should().BeTrue();
        capturedCorrelationId.Should().Be("new-correlation-id");
        capturedCausationId.Should().Be("causation-123");
    }

    [Fact]
    public async Task Should_Call_Next_Handler()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var middleware = new CorrelationMiddleware(accessor, NullLogger<CorrelationMiddleware>.Instance);
        
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var context = MessageContext.FromEnvelope(envelope);
        
        var nextCalled = false;
        var taskCompletionSource = new TaskCompletionSource<bool>();

        // Act
        await middleware.InvokeAsync(envelope, context, async () =>
        {
            nextCalled = true;
            await Task.Delay(10); // Simulate async work
            taskCompletionSource.SetResult(true);
        });

        // Assert
        nextCalled.Should().BeTrue();
        taskCompletionSource.Task.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Handle_Null_CausationId()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var middleware = new CorrelationMiddleware(accessor, NullLogger<CorrelationMiddleware>.Instance);
        
        var message = new TestMessage("test");
        var envelope = new MessageEnvelope<TestMessage>
        {
            Message = message,
            CorrelationId = "correlation-123",
            CausationId = null
        };
        
        var context = MessageContext.FromEnvelope(envelope);
        string? capturedCorrelationId = null;
        string? capturedCausationId = null;

        // Act
        await middleware.InvokeAsync(envelope, context, () => 
        {
            capturedCorrelationId = accessor.CorrelationId;
            capturedCausationId = accessor.CausationId;
            return Task.CompletedTask;
        });

        // Assert
        capturedCorrelationId.Should().Be("correlation-123");
        capturedCausationId.Should().BeNull();
    }

    [Fact]
    public async Task Should_Support_Cancellation()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var middleware = new CorrelationMiddleware(accessor, NullLogger<CorrelationMiddleware>.Instance);
        
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var context = MessageContext.FromEnvelope(envelope);
        var cts = new CancellationTokenSource();

        // Act & Assert
        await middleware.InvokeAsync(envelope, context, () => Task.CompletedTask, cts.Token);
        cts.Token.IsCancellationRequested.Should().BeFalse();
    }

    private record TestMessage(string Value);
}
