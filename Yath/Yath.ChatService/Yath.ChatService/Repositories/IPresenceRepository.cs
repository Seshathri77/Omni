using Yath.ChatService.Models;

namespace Yath.ChatService.Repositories;

public interface IPresenceRepository
{
    Task<UserPresence?> GetByUserAndRoomAsync(string userId, string roomId);
    Task<List<UserPresence>> GetByRoomIdAsync(string roomId);
    Task UpsertAsync(UserPresence presence);
    Task UpdateStatusAsync(string userId, string roomId, PresenceStatus status);
    Task UpdateConnectionIdAsync(string userId, string roomId, string? connectionId);
}
