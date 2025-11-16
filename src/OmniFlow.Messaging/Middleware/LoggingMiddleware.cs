using Microsoft.Extensions.Logging;
using OmniFlow.Core;
using System.Diagnostics;

namespace OmniFlow.Messaging.Middleware;

/// <summary>
/// Middleware that logs message processing with timing information.
/// </summary>
public class LoggingMiddleware : IMessageMiddleware
{
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(ILogger<LoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync<T>(
        MessageEnvelope<T> envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default) where T : class
    {
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation(
            "Processing message {MessageId} of type {MessageType}",
            envelope.MessageId,
            envelope.MessageType);

        try
        {
            await next();
            
            stopwatch.Stop();
            _logger.LogInformation(
                "Completed processing message {MessageId} in {ElapsedMs}ms",
                envelope.MessageId,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Failed processing message {MessageId} after {ElapsedMs}ms",
                envelope.MessageId,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
