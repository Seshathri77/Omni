using Microsoft.Extensions.DependencyInjection;

namespace OmniFlow.Sagas;

/// <summary>
/// Extension methods for registering OmniFlow.Sagas services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OmniFlow saga services with in-memory repositories.
    /// </summary>
    public static IServiceCollection AddOmniFlowSagas(this IServiceCollection services)
    {
        services.AddSingleton(typeof(ISagaRepository<>), typeof(InMemorySagaRepository<>));
        services.AddSingleton<ITimerService, InMemoryTimerService>();
        services.AddSingleton<IDistributedLock, InMemoryDistributedLock>();
        
        return services;
    }

    /// <summary>
    /// Adds SQL-based durable timer service with background processing.
    /// Requires ISagaDbContext to be registered (e.g., via AddOmniFlowSqlAdapters).
    /// </summary>
    public static IServiceCollection AddSqlTimerService(this IServiceCollection services)
    {
        services.AddSingleton<ITimerService, SqlTimerService>();
        services.AddHostedService<SqlTimerService>(sp => sp.GetRequiredService<ITimerService>() as SqlTimerService 
            ?? throw new InvalidOperationException("SqlTimerService not registered"));
        
        return services;
    }

    /// <summary>
    /// Adds SQL-based distributed lock for saga coordination.
    /// Requires ISagaDbContext to be registered (e.g., via AddOmniFlowSqlAdapters).
    /// </summary>
    public static IServiceCollection AddSqlDistributedLock(this IServiceCollection services)
    {
        services.AddSingleton<IDistributedLock, SqlDistributedLock>();
        return services;
    }

    /// <summary>
    /// Adds in-memory distributed lock (for development/testing).
    /// </summary>
    public static IServiceCollection AddInMemoryDistributedLock(this IServiceCollection services)
    {
        services.AddSingleton<IDistributedLock, InMemoryDistributedLock>();
        return services;
    }

    /// <summary>
    /// Registers a specific saga type.
    /// </summary>
    public static IServiceCollection AddSaga<TSaga, TState>(this IServiceCollection services)
        where TSaga : Saga<TState>
        where TState : SagaState, new()
    {
        services.AddTransient<TSaga>();
        return services;
    }

    /// <summary>
    /// Registers a specific saga type with configuration options.
    /// </summary>
    public static IServiceCollection AddSaga<TSaga, TState>(
        this IServiceCollection services,
        Action<SagaOptions<TState>> configureSaga)
        where TSaga : Saga<TState>
        where TState : SagaState, new()
    {
        services.AddTransient<TSaga>();
        
        var options = new SagaOptions<TState>();
        configureSaga?.Invoke(options);
        
        services.AddSingleton(options);
        return services;
    }
}

/// <summary>
/// Configuration options for a saga.
/// </summary>
public class SagaOptions<TState> where TState : SagaState, new()
{
    /// <summary>
    /// Maximum time a saga can run before timing out (default: none).
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Maximum number of retries for failed saga operations (default: 3).
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts (default: 1 second).
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether to automatically compensate on failure (default: true).
    /// </summary>
    public bool AutoCompensate { get; set; } = true;

    /// <summary>
    /// Custom state initialization logic.
    /// </summary>
    public Action<TState>? InitializeState { get; set; }

    /// <summary>
    /// Custom logging prefix for this saga type.
    /// </summary>
    public string? LoggingPrefix { get; set; }
}
