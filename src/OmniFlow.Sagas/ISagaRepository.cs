namespace OmniFlow.Sagas;

/// <summary>
/// Repository for persisting and retrieving saga state.
/// </summary>
/// <typeparam name="TState">The type of the saga state.</typeparam>
public interface ISagaRepository<TState> where TState : class
{
    /// <summary>
    /// Saves or updates saga state.
    /// </summary>
    Task SaveAsync(string sagaId, TState state, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves saga state by ID.
    /// </summary>
    Task<(TState State, int Version)?> GetAsync(string sagaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes saga state (after completion or cancellation).
    /// </summary>
    Task DeleteAsync(string sagaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds sagas by a correlation property.
    /// </summary>
    Task<IEnumerable<string>> FindByCorrelationAsync(string propertyName, string value, 
        CancellationToken cancellationToken = default);
}
