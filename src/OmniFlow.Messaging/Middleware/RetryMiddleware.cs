using Microsoft.Extensions.Logging;
using OmniFlow.Core;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace OmniFlow.Messaging.Middleware;

/// <summary>
/// Middleware that applies retry and circuit breaker policies using Polly.
/// </summary>
public class RetryMiddleware : IMessageMiddleware
{
    private readonly ILogger<RetryMiddleware> _logger;
    private readonly ResiliencePipeline _pipeline;

    public RetryMiddleware(ILogger<RetryMiddleware> logger, int maxRetries = 3)
        : this(logger, new RetryMiddlewareOptions { MaxRetries = maxRetries })
    {
    }

    public RetryMiddleware(ILogger<RetryMiddleware> logger, RetryMiddlewareOptions options)
    {
        _logger = logger;

        var pipelineBuilder = new ResiliencePipelineBuilder();

        // Add retry strategy
        pipelineBuilder.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = options.MaxRetries,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            OnRetry = args =>
            {
                _logger.LogWarning(
                    "Retry attempt {AttemptNumber} after {Delay}ms due to: {Exception}",
                    args.AttemptNumber,
                    args.RetryDelay.TotalMilliseconds,
                    args.Outcome.Exception?.Message);
                return ValueTask.CompletedTask;
            }
        });

        // Add circuit breaker if enabled
        if (options.EnableCircuitBreaker)
        {
            pipelineBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = options.CircuitBreakerFailureRatio,
                MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreakerSamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakerBreakDurationSeconds),
                OnOpened = args =>
                {
                    _logger.LogError(
                        "Circuit breaker opened due to high failure rate (will retry in {Seconds}s)",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Circuit breaker closed, resuming normal operation");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Circuit breaker half-opened, testing if service recovered");
                    return ValueTask.CompletedTask;
                }
            });
        }

        _pipeline = pipelineBuilder.Build();
    }

    public async Task InvokeAsync<T>(
        MessageEnvelope<T> envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default) where T : class
    {
        await _pipeline.ExecuteAsync(async ct => await next(), cancellationToken);
    }
}

/// <summary>
/// Configuration options for retry and circuit breaker middleware.
/// </summary>
public class RetryMiddlewareOptions
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Enable circuit breaker pattern.
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Circuit breaker failure ratio threshold (0.0 to 1.0).
    /// </summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Minimum number of requests before circuit breaker activates.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Sampling duration for calculating failure ratio (in seconds).
    /// </summary>
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// How long the circuit stays open before half-opening (in seconds).
    /// </summary>
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 30;
}
