using MongoDB.Driver;
using OmniFlow.Idempotency;

namespace OmniFlow.Adapters.MongoDb;

/// <summary>
/// MongoDB-based idempotency store for distributed scenarios.
/// </summary>
public class MongoDbIdempotencyStore : IIdempotencyStore
{
    private readonly IMongoCollection<IdempotencyRecord> _collection;

    /// <summary>
    /// Initializes a new instance of the MongoDbIdempotencyStore.
    /// </summary>
    /// <param name="database">MongoDB database instance.</param>
    /// <param name="collectionName">Name of the collection to store idempotency records. Default is "idempotency_records".</param>
    public MongoDbIdempotencyStore(IMongoDatabase database, string collectionName = "idempotency_records")
    {
        _collection = database.GetCollection<IdempotencyRecord>(collectionName);
        
        // Create compound unique index for atomic operations
        var indexKeysDefinition = Builders<IdempotencyRecord>.IndexKeys
            .Ascending(x => x.ConsumerName)
            .Ascending(x => x.MessageId);
        
        var indexOptions = new CreateIndexOptions { Unique = true };
        var indexModel = new CreateIndexModel<IdempotencyRecord>(indexKeysDefinition, indexOptions);
        
        _collection.Indexes.CreateOne(indexModel);

        // Create TTL index for automatic cleanup
        var ttlIndexKeysDefinition = Builders<IdempotencyRecord>.IndexKeys
            .Ascending(x => x.ExpiresAt);
        
        var ttlIndexOptions = new CreateIndexOptions 
        { 
            ExpireAfter = TimeSpan.Zero // MongoDB will delete documents when ExpiresAt is reached
        };
        var ttlIndexModel = new CreateIndexModel<IdempotencyRecord>(ttlIndexKeysDefinition, ttlIndexOptions);
        
        _collection.Indexes.CreateOne(ttlIndexModel);
    }

    /// <inheritdoc/>
    public async Task<bool> TryRecordAsync(
        string messageId,
        string consumerName,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        var record = new IdempotencyRecord
        {
            MessageId = messageId,
            ConsumerName = consumerName,
            ProcessedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(ttl ?? TimeSpan.FromDays(7))
        };

        try
        {
            // MongoDB will enforce the unique index and throw if duplicate
            await _collection.InsertOneAsync(record, cancellationToken: cancellationToken);
            return true; // Successfully inserted (first time)
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false; // Already exists (duplicate key error)
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(
        string messageId,
        string consumerName,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<IdempotencyRecord>.Filter.And(
            Builders<IdempotencyRecord>.Filter.Eq(x => x.MessageId, messageId),
            Builders<IdempotencyRecord>.Filter.Eq(x => x.ConsumerName, consumerName)
        );

        var count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        return count > 0;
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(
        string messageId,
        string consumerName,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<IdempotencyRecord>.Filter.And(
            Builders<IdempotencyRecord>.Filter.Eq(x => x.MessageId, messageId),
            Builders<IdempotencyRecord>.Filter.Eq(x => x.ConsumerName, consumerName)
        );

        await _collection.DeleteOneAsync(filter, cancellationToken);
    }

    /// <summary>
    /// Removes all expired records manually (MongoDB TTL index handles this automatically).
    /// </summary>
    public async Task CleanupExpiredRecordsAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<IdempotencyRecord>.Filter.Lt(x => x.ExpiresAt, DateTime.UtcNow);
        await _collection.DeleteManyAsync(filter, cancellationToken);
    }
}
