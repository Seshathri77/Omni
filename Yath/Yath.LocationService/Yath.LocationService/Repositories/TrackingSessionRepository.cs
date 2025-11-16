using MongoDB.Driver;
using Yath.LocationService.Models;

namespace Yath.LocationService.Repositories;

public class TrackingSessionRepository : ITrackingSessionRepository
{
    private readonly IMongoCollection<TrackingSession> _collection;

    public TrackingSessionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TrackingSession>("tracking_sessions");
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<TrackingSession>.IndexKeys;

        // Index on sessionId (unique)
        _collection.Indexes.CreateOne(
            new CreateIndexModel<TrackingSession>(
                indexKeys.Ascending(s => s.SessionId),
                new CreateIndexOptions { Unique = true }
            )
        );

        // Index on userId
        _collection.Indexes.CreateOne(
            new CreateIndexModel<TrackingSession>(
                indexKeys.Ascending(s => s.UserId)
            )
        );

        // Index on tripId
        _collection.Indexes.CreateOne(
            new CreateIndexModel<TrackingSession>(
                indexKeys.Ascending(s => s.TripId)
            )
        );

        // Compound index on userId + isActive (for finding active sessions)
        _collection.Indexes.CreateOne(
            new CreateIndexModel<TrackingSession>(
                indexKeys.Ascending(s => s.UserId).Ascending(s => s.IsActive)
            )
        );

        // Compound index on tripId + isActive
        _collection.Indexes.CreateOne(
            new CreateIndexModel<TrackingSession>(
                indexKeys.Ascending(s => s.TripId).Ascending(s => s.IsActive)
            )
        );
    }

    public async Task<TrackingSession?> GetByIdAsync(string sessionId)
    {
        return await _collection.Find(s => s.SessionId == sessionId).FirstOrDefaultAsync();
    }

    public async Task<TrackingSession?> GetActiveByUserIdAsync(string userId)
    {
        return await _collection
            .Find(s => s.UserId == userId && s.IsActive)
            .SortByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<TrackingSession>> GetByUserIdAsync(string userId)
    {
        return await _collection
            .Find(s => s.UserId == userId)
            .SortByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<List<TrackingSession>> GetByTripIdAsync(string tripId)
    {
        return await _collection
            .Find(s => s.TripId == tripId)
            .SortByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<List<TrackingSession>> GetActiveByTripIdAsync(string tripId)
    {
        return await _collection
            .Find(s => s.TripId == tripId && s.IsActive)
            .ToListAsync();
    }

    public async Task CreateAsync(TrackingSession session)
    {
        await _collection.InsertOneAsync(session);
    }

    public async Task UpdateAsync(TrackingSession session)
    {
        await _collection.ReplaceOneAsync(s => s.SessionId == session.SessionId, session);
    }

    public async Task EndSessionAsync(string sessionId)
    {
        var update = Builders<TrackingSession>.Update
            .Set(s => s.IsActive, false)
            .Set(s => s.EndedAt, DateTime.UtcNow);

        await _collection.UpdateOneAsync(s => s.SessionId == sessionId, update);
    }

    public async Task UpdateConnectionIdAsync(string sessionId, string? connectionId)
    {
        var update = Builders<TrackingSession>.Update
            .Set(s => s.ConnectionId, connectionId)
            .Set(s => s.LastUpdateAt, DateTime.UtcNow);

        await _collection.UpdateOneAsync(s => s.SessionId == sessionId, update);
    }
}
