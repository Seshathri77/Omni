using Microsoft.Extensions.Logging;
using OmniFlow.Core;
using Polly;
using Polly.Retry;

namespace OmniFlow.Messaging.Middleware;

/// <summary>
/// Middleware that applies retry policies using Polly.
/// </summary>
public class RetryMiddleware : IMessageMiddleware
{
    private readonly ILogger<RetryMiddleware> _logger;
    private readonly ResiliencePipeline _pipeline;

    public RetryMiddleware(ILogger<RetryMiddleware> logger, int maxRetries = 3)
    {
        _logger = logger;
        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
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
            })
            .Build();
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
