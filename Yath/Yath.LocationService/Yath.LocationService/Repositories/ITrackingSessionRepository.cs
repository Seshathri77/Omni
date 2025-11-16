using Yath.LocationService.Models;

namespace Yath.LocationService.Repositories;

public interface ITrackingSessionRepository
{
    Task<TrackingSession?> GetByIdAsync(string sessionId);
    Task<TrackingSession?> GetActiveByUserIdAsync(string userId);
    Task<List<TrackingSession>> GetByUserIdAsync(string userId);
    Task<List<TrackingSession>> GetByTripIdAsync(string tripId);
    Task<List<TrackingSession>> GetActiveByTripIdAsync(string tripId);
    Task CreateAsync(TrackingSession session);
    Task UpdateAsync(TrackingSession session);
    Task EndSessionAsync(string sessionId);
    Task UpdateConnectionIdAsync(string sessionId, string? connectionId);
}
