using Yath.TripService.Models;

namespace Yath.TripService.Repositories;

public interface ITripRepository
{
    Task<Trip?> GetByIdAsync(string tripId);
    Task<List<Trip>> GetByCreatorAsync(string creatorId, int skip = 0, int limit = 20);
    Task<List<Trip>> GetByParticipantAsync(string userId, int skip = 0, int limit = 20);
    Task CreateAsync(Trip trip);
    Task UpdateAsync(Trip trip);
    Task DeleteAsync(string tripId);
    Task<bool> ExistsAsync(string tripId);
    Task<bool> IsParticipantAsync(string tripId, string userId);
}
