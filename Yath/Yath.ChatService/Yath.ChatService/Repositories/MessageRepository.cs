using MongoDB.Driver;
using Yath.ChatService.Models;

namespace Yath.ChatService.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly IMongoCollection<Message> _messagesCollection;

    public MessageRepository(IMongoDatabase database)
    {
        _messagesCollection = database.GetCollection<Message>("messages");
        
        // Create indexes
        var messageIdIndex = Builders<Message>.IndexKeys.Ascending(m => m.MessageId);
        _messagesCollection.Indexes.CreateOne(new CreateIndexModel<Message>(messageIdIndex, 
            new CreateIndexOptions { Unique = true }));
        
        var roomIdIndex = Builders<Message>.IndexKeys.Ascending(m => m.RoomId);
        _messagesCollection.Indexes.CreateOne(new CreateIndexModel<Message>(roomIdIndex));
        
        var timestampIndex = Builders<Message>.IndexKeys.Descending(m => m.Timestamp);
        _messagesCollection.Indexes.CreateOne(new CreateIndexModel<Message>(timestampIndex));
        
        var compoundIndex = Builders<Message>.IndexKeys
            .Ascending(m => m.RoomId)
            .Descending(m => m.Timestamp);
        _messagesCollection.Indexes.CreateOne(new CreateIndexModel<Message>(compoundIndex));
    }

    public async Task<Message?> GetByIdAsync(string messageId)
    {
        return await _messagesCollection.Find(m => m.MessageId == messageId).FirstOrDefaultAsync();
    }

    public async Task<List<Message>> GetByRoomIdAsync(string roomId, int skip = 0, int limit = 50)
    {
        return await _messagesCollection
            .Find(m => m.RoomId == roomId && !m.IsDeleted)
            .SortByDescending(m => m.Timestamp)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<Message> CreateAsync(Message message)
    {
        await _messagesCollection.InsertOneAsync(message);
        return message;
    }

    public async Task UpdateAsync(Message message)
    {
        await _messagesCollection.ReplaceOneAsync(m => m.MessageId == message.MessageId, message);
    }

    public async Task DeleteAsync(string messageId)
    {
        var update = Builders<Message>.Update.Set(m => m.IsDeleted, true);
        await _messagesCollection.UpdateOneAsync(m => m.MessageId == messageId, update);
    }

    public async Task MarkAsReadAsync(string messageId, string userId)
    {
        var update = Builders<Message>.Update.AddToSet(m => m.ReadBy, userId);
        await _messagesCollection.UpdateOneAsync(m => m.MessageId == messageId, update);
    }

    public async Task AddReactionAsync(string messageId, string userId, string emoji)
    {
        var reaction = new MessageReaction
        {
            UserId = userId,
            Emoji = emoji,
            Timestamp = DateTime.UtcNow
        };
        
        // Remove existing reaction from same user with same emoji
        var pullUpdate = Builders<Message>.Update.PullFilter(
            m => m.Reactions,
            r => r.UserId == userId && r.Emoji == emoji
        );
        await _messagesCollection.UpdateOneAsync(m => m.MessageId == messageId, pullUpdate);
        
        // Add new reaction
        var pushUpdate = Builders<Message>.Update.Push(m => m.Reactions, reaction);
        await _messagesCollection.UpdateOneAsync(m => m.MessageId == messageId, pushUpdate);
    }

    public async Task RemoveReactionAsync(string messageId, string userId, string emoji)
    {
        var update = Builders<Message>.Update.PullFilter(
            m => m.Reactions,
            r => r.UserId == userId && r.Emoji == emoji
        );
        await _messagesCollection.UpdateOneAsync(m => m.MessageId == messageId, update);
    }
}
