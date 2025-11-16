using Yath.TripService.Models;

namespace Yath.TripService.Repositories;

public interface IItineraryRepository
{
    Task<List<Itinerary>> GetByTripIdAsync(string tripId);
    Task<Itinerary?> GetByDayAsync(string tripId, int day);
    Task CreateAsync(Itinerary itinerary);
    Task UpdateAsync(Itinerary itinerary);
    Task DeleteAsync(string itineraryId);
    Task DeleteByTripIdAsync(string tripId);
}
