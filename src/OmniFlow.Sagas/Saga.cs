using OmniFlow.Core;
using OmniFlow.Messaging;

namespace OmniFlow.Sagas;

/// <summary>
/// Base class for sagas with orchestration and compensation logic.
/// </summary>
/// <typeparam name="TState">The type of the saga state.</typeparam>
public abstract class Saga<TState> where TState : SagaState, new()
{
    protected ISagaRepository<TState> Repository { get; private set; } = null!;
    protected IMessageBus MessageBus { get; private set; } = null!;
    protected ITimerService? TimerService { get; private set; }

    /// <summary>
    /// Current saga state.
    /// </summary>
    protected TState State { get; private set; } = new();

    /// <summary>
    /// Initialize the saga with required services.
    /// </summary>
    public void Initialize(
        ISagaRepository<TState> repository,
        IMessageBus messageBus,
        ITimerService? timerService = null)
    {
        Repository = repository;
        MessageBus = messageBus;
        TimerService = timerService;
    }

    /// <summary>
    /// Starts a new saga instance.
    /// </summary>
    public async Task StartAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        State = new TState
        {
            SagaId = Guid.NewGuid().ToString(),
            CorrelationId = correlationId,
            Status = SagaStatus.Running
        };

        AddHistory($"Saga started");
        await SaveStateAsync(cancellationToken);
        await OnStartAsync(cancellationToken);
    }

    /// <summary>
    /// Starts a new saga instance with pre-initialized state.
    /// </summary>
    protected async Task StartAsync(TState initialState, CancellationToken cancellationToken = default)
    {
        // Only set SagaId if not already provided
        if (string.IsNullOrEmpty(initialState.SagaId))
            initialState.SagaId = Guid.NewGuid().ToString();
        
        initialState.Status = SagaStatus.Running;
        State = initialState;

        AddHistory($"Saga started");
        await SaveStateAsync(cancellationToken);
        await OnStartAsync(cancellationToken);
    }

    /// <summary>
    /// Loads an existing saga instance.
    /// </summary>
    public async Task<bool> LoadAsync(string sagaId, CancellationToken cancellationToken = default)
    {
        var result = await Repository.GetAsync(sagaId, cancellationToken);
        if (result == null)
            return false;

        State = result.Value.State;
        return true;
    }

    /// <summary>
    /// Publishes a message (command or event) and saves state.
    /// </summary>
    protected async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) 
        where T : class, IMessage
    {
        await MessageBus.PublishAsync(message, cancellationToken);
        AddHistory($"Published {typeof(T).Name}");
        State.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveStateAsync(cancellationToken);
    }

    /// <summary>
    /// Completes the saga successfully.
    /// </summary>
    protected async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        State.Status = SagaStatus.Completed;
        AddHistory("Saga completed");
        await SaveStateAsync(cancellationToken);
        await OnCompletedAsync(cancellationToken);
    }

    /// <summary>
    /// Initiates compensation (rollback) of the saga.
    /// </summary>
    protected async Task CompensateAsync(string reason, CancellationToken cancellationToken = default)
    {
        State.Status = SagaStatus.Compensating;
        AddHistory($"Starting compensation: {reason}");
        await SaveStateAsync(cancellationToken);
        await OnCompensateAsync(cancellationToken);
        
        State.Status = SagaStatus.Compensated;
        AddHistory("Compensation completed");
        await SaveStateAsync(cancellationToken);
    }

    /// <summary>
    /// Marks the saga as failed.
    /// </summary>
    protected async Task FailAsync(string reason, CancellationToken cancellationToken = default)
    {
        State.Status = SagaStatus.Failed;
        AddHistory($"Saga failed: {reason}");
        await SaveStateAsync(cancellationToken);
    }

    /// <summary>
    /// Schedules a durable timer.
    /// </summary>
    protected async Task<string> ScheduleTimerAsync(
        TimeSpan delay, 
        string timerName,
        CancellationToken cancellationToken = default)
    {
        if (TimerService == null)
            throw new InvalidOperationException("TimerService not configured");

        var timerId = await TimerService.ScheduleAsync(State.SagaId, delay, timerName, cancellationToken);
        AddHistory($"Scheduled timer '{timerName}' to fire in {delay}");
        return timerId;
    }

    /// <summary>
    /// Override to handle saga start logic.
    /// </summary>
    protected virtual Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Override to handle saga completion logic.
    /// </summary>
    protected virtual Task OnCompletedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Override to handle compensation logic (rollback).
    /// </summary>
    protected virtual Task OnCompensateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SaveStateAsync(CancellationToken cancellationToken)
    {
        State.Version++;
        State.UpdatedAt = DateTimeOffset.UtcNow;
        await Repository.SaveAsync(State.SagaId, State, State.Version, cancellationToken);
    }

    private void AddHistory(string entry)
    {
        State.History.Add($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {entry}");
    }
}
