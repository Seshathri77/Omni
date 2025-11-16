using Yath.LocationService.Models;

namespace Yath.LocationService.Repositories;

public interface ILocationUpdateRepository
{
    Task<LocationUpdate?> GetByIdAsync(string locationId);
    Task<List<LocationUpdate>> GetByUserIdAsync(string userId, int limit = 100);
    Task<List<LocationUpdate>> GetByTripIdAsync(string tripId, int limit = 100);
    Task<List<LocationUpdate>> GetBySessionIdAsync(string sessionId);
    Task<List<LocationUpdate>> GetRecentByUserIdAsync(string userId, TimeSpan timeWindow);
    Task<List<LocationUpdate>> GetInTimeRangeAsync(string userId, DateTime startTime, DateTime endTime);
    Task CreateAsync(LocationUpdate location);
    Task DeleteByUserIdAsync(string userId);
    Task DeleteBySessionIdAsync(string sessionId);
}
