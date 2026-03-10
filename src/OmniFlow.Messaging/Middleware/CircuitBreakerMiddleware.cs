using Microsoft.Extensions.Logging;
using OmniFlow.Core;
using Polly;
using Polly.CircuitBreaker;

namespace OmniFlow.Messaging.Middleware;

/// <summary>
/// Circuit breaker middleware for message processing to prevent cascading failures.
/// </summary>
public class CircuitBreakerMiddleware : IMessageMiddleware
{
    private readonly ILogger<CircuitBreakerMiddleware> _logger;
    private readonly ResiliencePipeline _pipeline;

    public CircuitBreakerMiddleware(
        ILogger<CircuitBreakerMiddleware> logger,
        CircuitBreakerMiddlewareOptions? options = null)
    {
        _logger = logger;
        options ??= new CircuitBreakerMiddlewareOptions();

        // Build resilience pipeline with circuit breaker
        _pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = options.FailureThreshold,
                SamplingDuration = options.SamplingDuration,
                MinimumThroughput = options.MinimumThroughput,
                BreakDuration = options.BreakDuration,
                OnOpened = args =>
                {
                    _logger.LogWarning(
                        "Circuit breaker opened after {FailureCount} failures. Breaking for {BreakDuration}",
                        args.Outcome.Exception?.GetType().Name ?? "unknown",
                        options.BreakDuration);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Circuit breaker closed. Normal operation resumed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Circuit breaker half-opened. Testing with limited requests");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task InvokeAsync<T>(
        MessageEnvelope<T> envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            await _pipeline.ExecuteAsync(async ct =>
            {
                await next();
            }, cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex,
                "Message {MessageId} rejected by circuit breaker. Circuit is open",
                envelope.MessageId);
            throw;
        }
    }
}

/// <summary>
/// Configuration options for circuit breaker middleware.
/// </summary>
public class CircuitBreakerMiddlewareOptions
{
    /// <summary>
    /// Failure threshold (0.0 to 1.0) before circuit opens. Default: 0.5 (50%)
    /// </summary>
    public double FailureThreshold { get; set; } = 0.5;

    /// <summary>
    /// Time window for sampling failures. Default: 30 seconds
    /// </summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Minimum number of requests before circuit can open. Default: 10
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Duration to keep circuit open before testing recovery. Default: 30 seconds
    /// </summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
