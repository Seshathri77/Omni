using Yath.ChatService.Models;

namespace Yath.ChatService.Repositories;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(string messageId);
    Task<List<Message>> GetByRoomIdAsync(string roomId, int skip = 0, int limit = 50);
    Task<Message> CreateAsync(Message message);
    Task UpdateAsync(Message message);
    Task DeleteAsync(string messageId);
    Task MarkAsReadAsync(string messageId, string userId);
    Task AddReactionAsync(string messageId, string userId, string emoji);
    Task RemoveReactionAsync(string messageId, string userId, string emoji);
}
