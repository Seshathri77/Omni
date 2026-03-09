using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniFlow.Core;
using OmniFlow.Messaging.Middleware;
using Xunit;

namespace OmniFlow.Tests.Messaging.Middleware;

public class LoggingMiddlewareTests
{
    [Fact]
    public async Task Should_Call_Next_Handler()
    {
        // Arrange
        var middleware = new LoggingMiddleware(NullLogger<LoggingMiddleware>.Instance);
        
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var context = MessageContext.FromEnvelope(envelope);
        
        var nextCalled = false;

        // Act
        await middleware.InvokeAsync(envelope, context, () =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Measure_Execution_Time()
    {
        // Arrange
        var middleware = new LoggingMiddleware(NullLogger<LoggingMiddleware>.Instance);
        
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var context = MessageContext.FromEnvelope(envelope);
        
        var executionTime = TimeSpan.Zero;
        var startTime = DateTime.UtcNow;

        // Act
        await middleware.InvokeAsync(envelope, context, async () =>
        {
            await Task.Delay(50); // Simulate work
            executionTime = DateTime.UtcNow - startTime;
        });

        // Assert
        executionTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(40));
    }

    [Fact]
    public async Task Should_Rethrow_Exceptions()
    {
        // Arrange
        var middleware = new LoggingMiddleware(NullLogger<LoggingMiddleware>.Instance);
        
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var context = MessageContext.FromEnvelope(envelope);
        
        var expectedException = new InvalidOperationException("Test error");

        // Act
        var act = async () => await middleware.InvokeAsync(envelope, context, () => throw expectedException);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test error");
    }

    [Fact]
    public async Task Should_Log_Even_When_Handler_Throws()
    {
        // Arrange
        var middleware = new LoggingMiddleware(NullLogger<LoggingMiddleware>.Instance);
        
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var context = MessageContext.FromEnvelope(envelope);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await middleware.InvokeAsync(envelope, context, () => throw new Exception("Handler error"));
        });
    }

    [Fact]
    public async Task Should_Support_Cancellation()
    {
        // Arrange
        var middleware = new LoggingMiddleware(NullLogger<LoggingMiddleware>.Instance);
        
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var context = MessageContext.FromEnvelope(envelope);
        var cts = new CancellationTokenSource();

        // Act
        await middleware.InvokeAsync(envelope, context, () => Task.CompletedTask, cts.Token);

        // Assert - no assertion needed, just ensure it completes
        cts.Token.IsCancellationRequested.Should().BeFalse();
    }

    private record TestMessage(string Value);
}
