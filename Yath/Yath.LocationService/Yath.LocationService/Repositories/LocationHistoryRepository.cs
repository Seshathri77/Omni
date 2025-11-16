using MongoDB.Driver;
using Yath.LocationService.Models;

namespace Yath.LocationService.Repositories;

public class LocationHistoryRepository : ILocationHistoryRepository
{
    private readonly IMongoCollection<LocationHistory> _collection;

    public LocationHistoryRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<LocationHistory>("location_history");
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<LocationHistory>.IndexKeys;

        // Index on historyId (unique)
        _collection.Indexes.CreateOne(
            new CreateIndexModel<LocationHistory>(
                indexKeys.Ascending(h => h.HistoryId),
                new CreateIndexOptions { Unique = true }
            )
        );

        // Index on userId
        _collection.Indexes.CreateOne(
            new CreateIndexModel<LocationHistory>(
                indexKeys.Ascending(h => h.UserId)
            )
        );

        // Index on tripId
        _collection.Indexes.CreateOne(
            new CreateIndexModel<LocationHistory>(
                indexKeys.Ascending(h => h.TripId)
            )
        );

        // Index on sessionId (unique)
        _collection.Indexes.CreateOne(
            new CreateIndexModel<LocationHistory>(
                indexKeys.Ascending(h => h.SessionId),
                new CreateIndexOptions { Unique = true }
            )
        );

        // Compound index on userId + startTime
        _collection.Indexes.CreateOne(
            new CreateIndexModel<LocationHistory>(
                indexKeys.Ascending(h => h.UserId).Descending(h => h.StartTime)
            )
        );
    }

    public async Task<LocationHistory?> GetByIdAsync(string historyId)
    {
        return await _collection.Find(h => h.HistoryId == historyId).FirstOrDefaultAsync();
    }

    public async Task<List<LocationHistory>> GetByUserIdAsync(string userId)
    {
        return await _collection
            .Find(h => h.UserId == userId)
            .SortByDescending(h => h.StartTime)
            .ToListAsync();
    }

    public async Task<List<LocationHistory>> GetByTripIdAsync(string tripId)
    {
        return await _collection
            .Find(h => h.TripId == tripId)
            .SortByDescending(h => h.StartTime)
            .ToListAsync();
    }

    public async Task<LocationHistory?> GetBySessionIdAsync(string sessionId)
    {
        return await _collection.Find(h => h.SessionId == sessionId).FirstOrDefaultAsync();
    }

    public async Task CreateAsync(LocationHistory history)
    {
        await _collection.InsertOneAsync(history);
    }

    public async Task UpdateAsync(LocationHistory history)
    {
        await _collection.ReplaceOneAsync(h => h.HistoryId == history.HistoryId, history);
    }

    public async Task DeleteAsync(string historyId)
    {
        await _collection.DeleteOneAsync(h => h.HistoryId == historyId);
    }
}
