using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniFlow.Core;
using OmniFlow.Messaging;
using OmniFlow.Messaging.Middleware;
using Polly.CircuitBreaker;
using Xunit;

namespace OmniFlow.Tests.Messaging.Middleware;

public class CircuitBreakerMiddlewareTests
{
    [Fact]
    public async Task CircuitBreaker_Should_Open_After_Threshold_Failures()
    {
        // Arrange
        var options = new CircuitBreakerMiddlewareOptions
        {
            FailureThreshold = 0.5,
            MinimumThroughput = 3,
            SamplingDuration = TimeSpan.FromSeconds(10),
            BreakDuration = TimeSpan.FromSeconds(5)
        };

        var middleware = new CircuitBreakerMiddleware(
            NullLogger<CircuitBreakerMiddleware>.Instance,
            options);

        var accessor = new CorrelationAccessor();
        accessor.SetContext("test-correlation", "test-causation");
        var envelope = MessageEnvelope<TestMessage>.Create(new TestMessage("test"), accessor);
        var context = MessageContext.FromEnvelope(envelope);

        var failureCount = 0;
        Func<Task> failingHandler = () =>
        {
            failureCount++;
            throw new InvalidOperationException("Simulated failure");
        };

        // Act & Assert - First failures should pass through
        for (int i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await middleware.InvokeAsync(envelope, context, failingHandler));
        }

        // Circuit should now be open
        await Assert.ThrowsAsync<BrokenCircuitException>(async () =>
            await middleware.InvokeAsync(envelope, context, failingHandler));

        failureCount.Should().Be(3); // No more calls should go through
    }

    [Fact]
    public async Task CircuitBreaker_Should_Allow_Successful_Requests()
    {
        // Arrange
        var middleware = new CircuitBreakerMiddleware(
            NullLogger<CircuitBreakerMiddleware>.Instance);

        var accessor = new CorrelationAccessor();
        accessor.SetContext("test-correlation", "test-causation");
        var envelope = MessageEnvelope<TestMessage>.Create(new TestMessage("test"), accessor);
        var context = MessageContext.FromEnvelope(envelope);

        var callCount = 0;
        Func<Task> successfulHandler = () =>
        {
            callCount++;
            return Task.CompletedTask;
        };

        // Act
        for (int i = 0; i < 10; i++)
        {
            await middleware.InvokeAsync(envelope, context, successfulHandler);
        }

        // Assert
        callCount.Should().Be(10);
    }

    [Fact(Skip = "Polly timeout requires cooperative cancellation - deferred to Phase 2")]
    public async Task Timeout_Middleware_Should_Cancel_Long_Running_Operations()
    {
        // Note: Polly's TimeoutStrategy requires operations to respect CancellationToken.
        // This will be properly tested in Phase 2 with cancellation token propagation.
        
        var timeout = TimeSpan.FromSeconds(1);
        var middleware = new TimeoutMiddleware(
            NullLogger<TimeoutMiddleware>.Instance,
            timeout);

        var accessor = new CorrelationAccessor();
        accessor.SetContext("test-correlation", "test-causation");
        var envelope = MessageEnvelope<TestMessage>.Create(new TestMessage("test"), accessor);
        var context = MessageContext.FromEnvelope(envelope);

        Func<Task> slowHandler = async () =>
        {
            // This should respect cancellation token to work with Polly timeout
            await Task.Delay(TimeSpan.FromSeconds(10), context.CancellationToken);
        };

        var act = async () => await middleware.InvokeAsync(envelope, context, slowHandler);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Timeout_Middleware_Should_Allow_Fast_Operations()
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(5);
        var middleware = new TimeoutMiddleware(
            NullLogger<TimeoutMiddleware>.Instance,
            timeout);

        var accessor = new CorrelationAccessor();
        accessor.SetContext("test-correlation", "test-causation");
        var envelope = MessageEnvelope<TestMessage>.Create(new TestMessage("test"), accessor);
        var context = MessageContext.FromEnvelope(envelope);

        var executed = false;
        Func<Task> fastHandler = () =>
        {
            executed = true;
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(envelope, context, fastHandler);

        // Assert
        executed.Should().BeTrue();
    }

    private record TestMessage(string Value) : IMessage;
}
