using System.Collections.Concurrent;

namespace OmniFlow.Sagas;

/// <summary>
/// In-memory timer service for testing and development.
/// </summary>
public class InMemoryTimerService : ITimerService
{
    private readonly ConcurrentDictionary<string, TimerState> _timers = new();

    public Task<string> ScheduleAsync(
        string sagaId,
        TimeSpan delay,
        string timerName,
        CancellationToken cancellationToken = default)
    {
        var timerId = Guid.NewGuid().ToString();
        var fireAt = DateTimeOffset.UtcNow.Add(delay);

        var timerState = new TimerState(timerId, sagaId, timerName, fireAt);
        _timers.TryAdd(timerId, timerState);

        // Schedule the timer to fire (simplified - in production use Quartz.NET or similar)
        _ = Task.Delay(delay, cancellationToken).ContinueWith(async _ =>
        {
            if (_timers.TryGetValue(timerId, out var state) && !state.IsCompleted)
            {
                state.IsCompleted = true;
                await OnTimerFiredAsync(timerId, sagaId, timerName);
            }
        }, cancellationToken);

        return Task.FromResult(timerId);
    }

    public Task CancelAsync(string timerId, CancellationToken cancellationToken = default)
    {
        if (_timers.TryGetValue(timerId, out var state))
        {
            state.IsCompleted = true;
        }
        return Task.CompletedTask;
    }

    public Task<IEnumerable<TimerInfo>> GetTimersAsync(string sagaId, CancellationToken cancellationToken = default)
    {
        var timers = _timers.Values
            .Where(t => t.SagaId == sagaId)
            .Select(t => new TimerInfo(t.TimerId, t.SagaId, t.TimerName, t.FireAt, t.IsCompleted));

        return Task.FromResult(timers);
    }

    /// <summary>
    /// Event raised when a timer fires.
    /// </summary>
    public event Func<string, string, string, Task>? TimerFired;

    private async Task OnTimerFiredAsync(string timerId, string sagaId, string timerName)
    {
        if (TimerFired != null)
        {
            await TimerFired(timerId, sagaId, timerName);
        }
    }

    private class TimerState
    {
        public string TimerId { get; }
        public string SagaId { get; }
        public string TimerName { get; }
        public DateTimeOffset FireAt { get; }
        public bool IsCompleted { get; set; }

        public TimerState(string timerId, string sagaId, string timerName, DateTimeOffset fireAt)
        {
            TimerId = timerId;
            SagaId = sagaId;
            TimerName = timerName;
            FireAt = fireAt;
        }
    }
}
