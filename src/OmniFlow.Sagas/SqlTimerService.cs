using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OmniFlow.Messaging;
using OmniFlow.Core;
using Microsoft.EntityFrameworkCore;

namespace OmniFlow.Sagas;

/// <summary>
/// SQL-based durable timer service with background processing.
/// </summary>
public class SqlTimerService : ITimerService, IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SqlTimerService> _logger;
    private Timer? _pollingTimer;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);

    public SqlTimerService(
        IServiceProvider serviceProvider,
        ILogger<SqlTimerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<string> ScheduleAsync(
        string sagaId,
        TimeSpan delay,
        string timerName,
        CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ISagaDbContext>();

        var timerId = Guid.NewGuid().ToString();
        var fireAt = DateTimeOffset.UtcNow.Add(delay);

        var timer = new SagaTimer
        {
            TimerId = timerId,
            SagaId = sagaId,
            TimerName = timerName,
            FireAt = fireAt,
            CreatedAt = DateTimeOffset.UtcNow,
            IsCompleted = false
        };

        context.SagaTimers.Add(timer);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Scheduled timer {TimerId} '{TimerName}' for saga {SagaId} to fire at {FireAt}",
            timerId, timerName, sagaId, fireAt);

        return timerId;
    }

    public async Task CancelAsync(string timerId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ISagaDbContext>();

        var timer = await context.SagaTimers.FindAsync(new object[] { timerId }, cancellationToken);
        if (timer != null && !timer.IsCompleted)
        {
            timer.IsCompleted = true;
            timer.CancelledAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Cancelled timer {TimerId}", timerId);
        }
    }

    public async Task<IEnumerable<TimerInfo>> GetTimersAsync(
        string sagaId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ISagaDbContext>();

        var timers = await context.SagaTimers
            .Where(t => t.SagaId == sagaId)
            .ToListAsync(cancellationToken);

        return timers.Select(t => new TimerInfo(
            t.TimerId,
            t.SagaId,
            t.TimerName,
            t.FireAt,
            t.IsCompleted));
    }

    // IHostedService implementation for background timer processing
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SqlTimerService background processor starting");
        _pollingTimer = new Timer(
            ProcessDueTimers,
            null,
            TimeSpan.Zero,
            _pollingInterval);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SqlTimerService background processor stopping");
        _pollingTimer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async void ProcessDueTimers(object? state)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ISagaDbContext>();
            var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            // Get all timers that are due
            var dueTimers = await context.SagaTimers
                .Where(t => !t.IsCompleted && t.FireAt <= DateTimeOffset.UtcNow)
                .ToListAsync();

            if (dueTimers.Any())
            {
                _logger.LogInformation("Processing {Count} due timers", dueTimers.Count);
            }

            foreach (var timer in dueTimers)
            {
                try
                {
                    // Publish timer fired event
                    var timerEvent = new SagaTimerFired(
                        timer.SagaId,
                        timer.TimerName,
                        timer.TimerId);

                    await messageBus.PublishAsync(timerEvent);

                    // Mark timer as completed
                    timer.IsCompleted = true;
                    timer.FiredAt = DateTimeOffset.UtcNow;

                    _logger.LogInformation(
                        "Timer {TimerId} '{TimerName}' fired for saga {SagaId}",
                        timer.TimerId, timer.TimerName, timer.SagaId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error firing timer {TimerId} for saga {SagaId}",
                        timer.TimerId, timer.SagaId);
                }
            }

            if (dueTimers.Any())
            {
                await context.SaveChangesAsync();
            }

            // Cleanup old completed timers (older than 7 days)
            await CleanupOldTimers(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in timer processing loop");
        }
    }

    private async Task CleanupOldTimers(ISagaDbContext context)
    {
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-7);
        var oldTimers = await context.SagaTimers
            .Where(t => t.IsCompleted && t.FiredAt < cutoffDate)
            .ToListAsync();

        if (oldTimers.Any())
        {
            context.SagaTimers.RemoveRange(oldTimers);
            await context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} old completed timers", oldTimers.Count);
        }
    }

    public void Dispose()
    {
        _pollingTimer?.Dispose();
    }
}

/// <summary>
/// Event published when a saga timer fires.
/// </summary>
public record SagaTimerFired(
    string SagaId,
    string TimerName,
    string TimerId) : IEvent;

