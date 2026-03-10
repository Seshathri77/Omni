using Microsoft.Extensions.Logging;
using OmniFlow.Core;
using Polly;
using Polly.Timeout;

namespace OmniFlow.Messaging.Middleware;

/// <summary>
/// Timeout middleware for message processing to prevent hanging operations.
/// </summary>
public class TimeoutMiddleware : IMessageMiddleware
{
    private readonly ILogger<TimeoutMiddleware> _logger;
    private readonly ResiliencePipeline _pipeline;
    private readonly TimeSpan _timeout;

    public TimeoutMiddleware(
        ILogger<TimeoutMiddleware> logger,
        TimeSpan? timeout = null)
    {
        _logger = logger;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);

        _pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = _timeout,
                OnTimeout = args =>
                {
                    _logger.LogWarning(
                        "Message processing timed out after {Timeout}",
                        _timeout);
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
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex,
                "Message {MessageId} processing timed out after {Timeout}",
                envelope.MessageId, _timeout);
            throw;
        }
    }
}
