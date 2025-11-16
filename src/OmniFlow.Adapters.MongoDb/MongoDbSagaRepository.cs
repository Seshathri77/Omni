using MongoDB.Bson;
using MongoDB.Driver;
using OmniFlow.Sagas;

namespace OmniFlow.Adapters.MongoDb;

/// <summary>
/// MongoDB-based saga repository for distributed scenarios.
/// </summary>
public class MongoDbSagaRepository<TState> : ISagaRepository<TState>
    where TState : class
{
    private readonly IMongoCollection<SagaDocument<TState>> _collection;

    /// <summary>
    /// Initializes a new instance of the MongoDbSagaRepository.
    /// </summary>
    /// <param name="database">MongoDB database instance.</param>
    /// <param name="collectionName">Name of the collection to store saga states. Default is "saga_states".</param>
    public MongoDbSagaRepository(IMongoDatabase database, string collectionName = "saga_states")
    {
        _collection = database.GetCollection<SagaDocument<TState>>(collectionName);
        
        // Create indexes for efficient queries
        var sagaIdIndex = Builders<SagaDocument<TState>>.IndexKeys.Ascending(x => x.SagaId);
        _collection.Indexes.CreateOne(new CreateIndexModel<SagaDocument<TState>>(sagaIdIndex, new CreateIndexOptions { Unique = true }));

        var sagaTypeIndex = Builders<SagaDocument<TState>>.IndexKeys.Ascending(x => x.SagaType);
        _collection.Indexes.CreateOne(new CreateIndexModel<SagaDocument<TState>>(sagaTypeIndex));
    }

    /// <inheritdoc/>
    public async Task<(TState State, int Version)?> GetAsync(string sagaId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<SagaDocument<TState>>.Filter.Eq(x => x.SagaId, sagaId);
        var document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        
        if (document == null)
            return null;

        return (document.State, document.Version);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(string sagaId, TState state, int version, CancellationToken cancellationToken = default)
    {
        var document = new SagaDocument<TState>
        {
            SagaId = sagaId,
            SagaType = typeof(TState).Name,
            State = state,
            Version = version,
            UpdatedAt = DateTime.UtcNow
        };

        var filter = Builders<SagaDocument<TState>>.Filter.And(
            Builders<SagaDocument<TState>>.Filter.Eq(x => x.SagaId, sagaId),
            Builders<SagaDocument<TState>>.Filter.Eq(x => x.Version, version - 1) // Optimistic concurrency
        );

        var updateResult = await _collection.ReplaceOneAsync(
            filter,
            document,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);

        if (updateResult.MatchedCount == 0)
        {
            // Either document doesn't exist or version mismatch
            var existingDoc = await _collection.Find(Builders<SagaDocument<TState>>.Filter.Eq(x => x.SagaId, sagaId))
                .FirstOrDefaultAsync(cancellationToken);

            if (existingDoc != null)
            {
                // Version mismatch - concurrent update detected
                throw new InvalidOperationException(
                    $"Saga state version mismatch for {sagaId}. Expected version {version - 1}, but found {existingDoc.Version}. " +
                    "This indicates a concurrent update conflict.");
            }

            // Document doesn't exist, insert new
            document.CreatedAt = DateTime.UtcNow;
            await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string sagaId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<SagaDocument<TState>>.Filter.Eq(x => x.SagaId, sagaId);
        await _collection.DeleteOneAsync(filter, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> FindByCorrelationAsync(
        string propertyName,
        string value,
        CancellationToken cancellationToken = default)
    {
        // Build dynamic filter for nested property path in State document
        var filterDefinition = Builders<SagaDocument<TState>>.Filter.Eq($"state.{propertyName}", value);
        
        var documents = await _collection.Find(filterDefinition).ToListAsync(cancellationToken);
        return documents.Select(d => d.SagaId);
    }

    /// <summary>
    /// Lists all saga states with optional filtering (extension method, not part of interface).
    /// </summary>
    public async Task<IEnumerable<(string SagaId, TState State, int Version)>> ListAsync(
        string? sagaType = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var filterBuilder = Builders<SagaDocument<TState>>.Filter;
        var filter = !string.IsNullOrEmpty(sagaType)
            ? filterBuilder.Eq(x => x.SagaType, sagaType)
            : filterBuilder.Empty;

        var query = _collection.Find(filter).SortByDescending(x => x.UpdatedAt);

        List<SagaDocument<TState>> documents;
        if (limit.HasValue)
        {
            documents = await query.Limit(limit.Value).ToListAsync(cancellationToken);
        }
        else
        {
            documents = await query.ToListAsync(cancellationToken);
        }

        return documents.Select(d => (d.SagaId, d.State, d.Version));
    }
}
