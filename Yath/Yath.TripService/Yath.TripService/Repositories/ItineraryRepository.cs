using MongoDB.Driver;
using Yath.TripService.Models;

namespace Yath.TripService.Repositories;

public class ItineraryRepository : IItineraryRepository
{
    private readonly IMongoCollection<Itinerary> _itineraries;

    public ItineraryRepository(IMongoDatabase database)
    {
        _itineraries = database.GetCollection<Itinerary>("itineraries");

        // Create indexes
        var tripIdIndex = Builders<Itinerary>.IndexKeys.Ascending(i => i.TripId);
        _itineraries.Indexes.CreateOne(new CreateIndexModel<Itinerary>(tripIdIndex));

        var compoundIndex = Builders<Itinerary>.IndexKeys
            .Ascending(i => i.TripId)
            .Ascending(i => i.Day);
        _itineraries.Indexes.CreateOne(new CreateIndexModel<Itinerary>(compoundIndex,
            new CreateIndexOptions { Unique = true }));
    }

    public async Task<List<Itinerary>> GetByTripIdAsync(string tripId)
    {
        return await _itineraries.Find(i => i.TripId == tripId)
            .SortBy(i => i.Day)
            .ToListAsync();
    }

    public async Task<Itinerary?> GetByDayAsync(string tripId, int day)
    {
        return await _itineraries.Find(i => i.TripId == tripId && i.Day == day)
            .FirstOrDefaultAsync();
    }

    public async Task CreateAsync(Itinerary itinerary)
    {
        await _itineraries.InsertOneAsync(itinerary);
    }

    public async Task UpdateAsync(Itinerary itinerary)
    {
        itinerary.UpdatedAt = DateTime.UtcNow;
        await _itineraries.ReplaceOneAsync(i => i.ItineraryId == itinerary.ItineraryId, itinerary);
    }

    public async Task DeleteAsync(string itineraryId)
    {
        await _itineraries.DeleteOneAsync(i => i.ItineraryId == itineraryId);
    }

    public async Task DeleteByTripIdAsync(string tripId)
    {
        await _itineraries.DeleteManyAsync(i => i.TripId == tripId);
    }
}
