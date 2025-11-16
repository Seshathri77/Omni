using Yath.LocationService.Models;

namespace Yath.LocationService.Repositories;

public interface ILocationHistoryRepository
{
    Task<LocationHistory?> GetByIdAsync(string historyId);
    Task<List<LocationHistory>> GetByUserIdAsync(string userId);
    Task<List<LocationHistory>> GetByTripIdAsync(string tripId);
    Task<LocationHistory?> GetBySessionIdAsync(string sessionId);
    Task CreateAsync(LocationHistory history);
    Task UpdateAsync(LocationHistory history);
    Task DeleteAsync(string historyId);
}
