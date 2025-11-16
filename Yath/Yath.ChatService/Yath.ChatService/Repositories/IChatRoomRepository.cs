using Yath.ChatService.Models;

namespace Yath.ChatService.Repositories;

public interface IChatRoomRepository
{
    Task<ChatRoom?> GetByIdAsync(string roomId);
    Task<ChatRoom?> GetByTripIdAsync(string tripId);
    Task<List<ChatRoom>> GetByUserIdAsync(string userId);
    Task<ChatRoom> CreateAsync(ChatRoom room);
    Task UpdateAsync(ChatRoom room);
    Task AddParticipantAsync(string roomId, string userId);
    Task RemoveParticipantAsync(string roomId, string userId);
}
