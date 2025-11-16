using MongoDB.Driver;
using Yath.LocationService.Models;

namespace Yath.LocationService.Repositories;

public class LocationUpdateRepository : ILocationUpdateRepository
{
    private readonly IMongoCollection<LocationUpdate> _collection;

    public LocationUpdateRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<LocationUpdate>("location_updates");
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<LocationUpdate>.IndexKeys;

        // Index on locationId (unique)
        _collection.Indexes.CreateOne(
            new CreateIndexModel<LocationUpdate>(
                indexKeys.Ascending(l => l.LocationId),
                new CreateIndexOptions { Unique = true }
            )
        );

        // Index on userId
        _collection.Indexes.CreateOne(
            new CreateIndexModel<LocationUpdate>(
                indexKeys.Ascending(l => l.UserId)
            )
        );

        // Index on tripId
        _collection.Indexes.CreateOne(
            new CreateIndexModel<LocationUpdate>(
                indexKeys.Ascending(l => l.TripId)
            )
        );

        // Compound index on userId + timestamp (for time-based queries)
        _collection.Indexes.CreateOne(
            new CreateIndexModel<LocationUpdate>(
                indexKeys.Ascending(l => l.UserId).Descending(l => l.Timestamp)
            )
        );

        // Compound index on tripId + timestamp
        _collection.Indexes.CreateOne(
            new CreateIndexModel<LocationUpdate>(
                indexKeys.Ascending(l => l.TripId).Descending(l => l.Timestamp)
            )
        );

        // TTL index - auto-delete old location updates after 90 days
        _collection.Indexes.CreateOne(
            new CreateIndexModel<LocationUpdate>(
                indexKeys.Ascending(l => l.Timestamp),
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(90) }
            )
        );
    }

    public async Task<LocationUpdate?> GetByIdAsync(string locationId)
    {
        return await _collection.Find(l => l.LocationId == locationId).FirstOrDefaultAsync();
    }

    public async Task<List<LocationUpdate>> GetByUserIdAsync(string userId, int limit = 100)
    {
        return await _collection
            .Find(l => l.UserId == userId)
            .SortByDescending(l => l.Timestamp)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<LocationUpdate>> GetByTripIdAsync(string tripId, int limit = 100)
    {
        return await _collection
            .Find(l => l.TripId == tripId)
            .SortByDescending(l => l.Timestamp)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<LocationUpdate>> GetBySessionIdAsync(string sessionId)
    {
        return await _collection
            .Find(l => l.LocationId.StartsWith(sessionId)) // Assuming locationId contains sessionId
            .SortBy(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<LocationUpdate>> GetRecentByUserIdAsync(string userId, TimeSpan timeWindow)
    {
        var cutoffTime = DateTime.UtcNow - timeWindow;
        return await _collection
            .Find(l => l.UserId == userId && l.Timestamp >= cutoffTime)
            .SortByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<LocationUpdate>> GetInTimeRangeAsync(string userId, DateTime startTime, DateTime endTime)
    {
        return await _collection
            .Find(l => l.UserId == userId && l.Timestamp >= startTime && l.Timestamp <= endTime)
            .SortBy(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task CreateAsync(LocationUpdate location)
    {
        await _collection.InsertOneAsync(location);
    }

    public async Task DeleteByUserIdAsync(string userId)
    {
        await _collection.DeleteManyAsync(l => l.UserId == userId);
    }

    public async Task DeleteBySessionIdAsync(string sessionId)
    {
        await _collection.DeleteManyAsync(l => l.LocationId.StartsWith(sessionId));
    }
}
