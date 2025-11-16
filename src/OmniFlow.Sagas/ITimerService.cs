namespace OmniFlow.Sagas;

/// <summary>
/// Service for scheduling and managing durable timers.
/// </summary>
public interface ITimerService
{
    /// <summary>
    /// Schedules a timer that will fire after the specified delay.
    /// </summary>
    Task<string> ScheduleAsync(
        string sagaId,
        TimeSpan delay,
        string timerName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a scheduled timer.
    /// </summary>
    Task CancelAsync(string timerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active timers for a saga.
    /// </summary>
    Task<IEnumerable<TimerInfo>> GetTimersAsync(string sagaId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about a scheduled timer.
/// </summary>
public record TimerInfo(
    string TimerId,
    string SagaId,
    string TimerName,
    DateTimeOffset FireAt,
    bool IsCompleted);
