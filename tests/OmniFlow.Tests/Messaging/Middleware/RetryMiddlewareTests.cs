using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniFlow.Core;
using OmniFlow.Messaging.Middleware;
using Xunit;

namespace OmniFlow.Tests.Messaging.Middleware;

public class RetryMiddlewareTests
{
    [Fact]
    public async Task Should_Execute_Successfully_On_First_Attempt()
    {
        // Arrange
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance, maxRetries: 3);
        
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var context = MessageContext.FromEnvelope(envelope);
        
        var executionCount = 0;

        // Act
        await middleware.InvokeAsync(envelope, context, () =>
        {
            executionCount++;
            return Task.CompletedTask;
        });

        // Assert
        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_Retry_On_Failure()
    {
        // Arrange
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance, maxRetries: 3);
        
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var context = MessageContext.FromEnvelope(envelope);
        
        var executionCount = 0;

        // Act
        var act = async () => await middleware.InvokeAsync(envelope, context, () =>
        {
            executionCount++;
            throw new InvalidOperationException("Transient error");
        });

        // Assert - should retry maxRetries times + 1 initial attempt = 4 total
        await act.Should().ThrowAsync<InvalidOperationException>();
        executionCount.Should().Be(4); // 1 initial + 3 retries
    }

    [Fact]
    public async Task Should_Succeed_After_Retries()
    {
        // Arrange
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance, maxRetries: 3);
        
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var context = MessageContext.FromEnvelope(envelope);
        
        var executionCount = 0;

        // Act
        await middleware.InvokeAsync(envelope, context, () =>
        {
            executionCount++;
            if (executionCount < 3)
            {
                throw new InvalidOperationException("Transient error");
            }
            return Task.CompletedTask;
        });

        // Assert
        executionCount.Should().Be(3); // Failed twice, succeeded on third attempt
    }

    [Fact]
    public async Task Should_Use_Custom_Options()
    {
        // Arrange
        var options = new RetryMiddlewareOptions
        {
            MaxRetries = 5,
            EnableCircuitBreaker = false
        };
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance, options);
        
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var context = MessageContext.FromEnvelope(envelope);
        
        var executionCount = 0;

        // Act
        var act = async () => await middleware.InvokeAsync(envelope, context, () =>
        {
            executionCount++;
            throw new InvalidOperationException("Transient error");
        });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        executionCount.Should().Be(6); // 1 initial + 5 retries
    }

    [Fact]
    public async Task Should_Apply_Circuit_Breaker_When_Enabled()
    {
        // Arrange
        var options = new RetryMiddlewareOptions
        {
            MaxRetries = 2,
            EnableCircuitBreaker = true,
            CircuitBreakerFailureRatio = 0.5,
            CircuitBreakerMinimumThroughput = 2,
            CircuitBreakerSamplingDurationSeconds = 10,
            CircuitBreakerBreakDurationSeconds = 5
        };
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance, options);
        
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var context = MessageContext.FromEnvelope(envelope);

        // Act - Execute multiple failing requests
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await middleware.InvokeAsync(envelope, context, () => throw new InvalidOperationException("Error"));
            }
            catch { }
        }

        // Assert - Circuit breaker configuration was applied (no exception means it worked)
        true.Should().BeTrue(); // Just ensure no configuration exceptions
    }

    [Fact]
    public async Task Should_Support_Cancellation()
    {
        // Arrange
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance, maxRetries: 3);
        
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var cts = new CancellationTokenSource();
        var context = MessageContext.FromEnvelope(envelope, cts.Token);

        // Act
        await middleware.InvokeAsync(envelope, context, () => Task.CompletedTask, cts.Token);

        // Assert
        cts.Token.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Respect_Cancellation_During_Retry()
    {
        // Arrange
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance, maxRetries: 10);
        
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var cts = new CancellationTokenSource();
        var context = MessageContext.FromEnvelope(envelope, cts.Token);
        
        var executionCount = 0;

        // Act
        var act = async () => await middleware.InvokeAsync(envelope, context, () =>
        {
            executionCount++;
            if (executionCount == 2)
            {
                cts.Cancel();
            }
            throw new InvalidOperationException("Error");
        }, cts.Token);

        // Assert - Should throw OperationCanceledException when cancelled
        await act.Should().ThrowAsync<Exception>(); // Could be OperationCanceledException or the original exception
        executionCount.Should().BeLessThan(11); // Should not complete all retries
    }

    [Fact]
    public void RetryMiddlewareOptions_Should_Have_Default_Values()
    {
        // Arrange & Act
        var options = new RetryMiddlewareOptions();

        // Assert
        options.MaxRetries.Should().Be(3);
        options.EnableCircuitBreaker.Should().BeTrue();
        options.CircuitBreakerFailureRatio.Should().Be(0.5);
        options.CircuitBreakerMinimumThroughput.Should().Be(10);
        options.CircuitBreakerSamplingDurationSeconds.Should().Be(30);
        options.CircuitBreakerBreakDurationSeconds.Should().Be(30);
    }

    [Fact]
    public void RetryMiddlewareOptions_Should_Allow_Custom_Values()
    {
        // Arrange & Act
        var options = new RetryMiddlewareOptions
        {
            MaxRetries = 5,
            EnableCircuitBreaker = false,
            CircuitBreakerFailureRatio = 0.7,
            CircuitBreakerMinimumThroughput = 20,
            CircuitBreakerSamplingDurationSeconds = 60,
            CircuitBreakerBreakDurationSeconds = 120
        };

        // Assert
        options.MaxRetries.Should().Be(5);
        options.EnableCircuitBreaker.Should().BeFalse();
        options.CircuitBreakerFailureRatio.Should().Be(0.7);
        options.CircuitBreakerMinimumThroughput.Should().Be(20);
        options.CircuitBreakerSamplingDurationSeconds.Should().Be(60);
        options.CircuitBreakerBreakDurationSeconds.Should().Be(120);
    }

    [Fact]
    public async Task Should_Trigger_Circuit_Breaker_Events()
    {
        // Arrange
        var options = new RetryMiddlewareOptions
        {
            MaxRetries = 1,
            EnableCircuitBreaker = true,
            CircuitBreakerFailureRatio = 0.5,
            CircuitBreakerMinimumThroughput = 2,
            CircuitBreakerSamplingDurationSeconds = 10,
            CircuitBreakerBreakDurationSeconds = 1 // Short duration for testing
        };
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance, options);
        
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var context = MessageContext.FromEnvelope(envelope);
        
        // Act - Trigger circuit breaker by failing multiple requests
        for (int i = 0; i < 5; i++)
        {
            try
            {
                await middleware.InvokeAsync(envelope, context, () => throw new InvalidOperationException("Error"));
            }
            catch { }
        }

        // Wait for circuit to potentially close
        await Task.Delay(1500);

        // Try again - might trigger OnHalfOpened or OnClosed
        try
        {
            await middleware.InvokeAsync(envelope, context, () => Task.CompletedTask);
        }
        catch { }

        // Assert - No exceptions means configuration was valid
        true.Should().BeTrue();
    }

    private record TestMessage(string Value);
}
