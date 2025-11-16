using MongoDB.Driver;
using Yath.TripService.Models;

namespace Yath.TripService.Repositories;

public class TripRepository : ITripRepository
{
    private readonly IMongoCollection<Trip> _trips;

    public TripRepository(IMongoDatabase database)
    {
        _trips = database.GetCollection<Trip>("trips");

        // Create indexes
        var tripIdIndex = Builders<Trip>.IndexKeys.Ascending(t => t.TripId);
        _trips.Indexes.CreateOne(new CreateIndexModel<Trip>(tripIdIndex,
            new CreateIndexOptions { Unique = true }));

        var creatorIndex = Builders<Trip>.IndexKeys.Ascending(t => t.CreatorId);
        _trips.Indexes.CreateOne(new CreateIndexModel<Trip>(creatorIndex));

        var statusIndex = Builders<Trip>.IndexKeys.Ascending(t => t.Status);
        _trips.Indexes.CreateOne(new CreateIndexModel<Trip>(statusIndex));
    }

    public async Task<Trip?> GetByIdAsync(string tripId)
    {
        return await _trips.Find(t => t.TripId == tripId).FirstOrDefaultAsync();
    }

    public async Task<List<Trip>> GetByCreatorAsync(string creatorId, int skip = 0, int limit = 20)
    {
        return await _trips.Find(t => t.CreatorId == creatorId)
            .SortByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<Trip>> GetByParticipantAsync(string userId, int skip = 0, int limit = 20)
    {
        var filter = Builders<Trip>.Filter.ElemMatch(
            t => t.Participants,
            p => p.UserId == userId
        );

        return await _trips.Find(filter)
            .SortByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task CreateAsync(Trip trip)
    {
        await _trips.InsertOneAsync(trip);
    }

    public async Task UpdateAsync(Trip trip)
    {
        trip.UpdatedAt = DateTime.UtcNow;
        await _trips.ReplaceOneAsync(t => t.TripId == trip.TripId, trip);
    }

    public async Task DeleteAsync(string tripId)
    {
        await _trips.DeleteOneAsync(t => t.TripId == tripId);
    }

    public async Task<bool> ExistsAsync(string tripId)
    {
        return await _trips.Find(t => t.TripId == tripId).AnyAsync();
    }

    public async Task<bool> IsParticipantAsync(string tripId, string userId)
    {
        var filter = Builders<Trip>.Filter.And(
            Builders<Trip>.Filter.Eq(t => t.TripId, tripId),
            Builders<Trip>.Filter.ElemMatch(t => t.Participants, p => p.UserId == userId)
        );

        return await _trips.Find(filter).AnyAsync();
    }
}
