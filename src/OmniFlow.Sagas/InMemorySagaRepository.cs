using System.Collections.Concurrent;

namespace OmniFlow.Sagas;

/// <summary>
/// In-memory saga repository for testing and development.
/// </summary>
public class InMemorySagaRepository<TState> : ISagaRepository<TState> where TState : SagaState
{
    private readonly ConcurrentDictionary<string, (TState State, int Version)> _store = new();

    public Task SaveAsync(string sagaId, TState state, int version, CancellationToken cancellationToken = default)
    {
        _store.AddOrUpdate(
            sagaId,
            (state, version),
            (_, _) => (state, version));
        
        return Task.CompletedTask;
    }

    public Task<(TState State, int Version)?> GetAsync(string sagaId, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(sagaId, out var result))
        {
            return Task.FromResult<(TState State, int Version)?>(result);
        }
        
        return Task.FromResult<(TState State, int Version)?>(null);
    }

    public Task DeleteAsync(string sagaId, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(sagaId, out _);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> FindByCorrelationAsync(
        string propertyName, 
        string value,
        CancellationToken cancellationToken = default)
    {
        // Simple implementation - in production, use indexed queries
        var matches = _store
            .Where(kvp => kvp.Value.State.CorrelationId == value)
            .Select(kvp => kvp.Key);
        
        return Task.FromResult(matches);
    }
}
