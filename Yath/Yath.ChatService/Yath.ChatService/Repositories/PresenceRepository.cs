using MongoDB.Driver;
using Yath.ChatService.Models;

namespace Yath.ChatService.Repositories;

public class PresenceRepository : IPresenceRepository
{
    private readonly IMongoCollection<UserPresence> _presenceCollection;

    public PresenceRepository(IMongoDatabase database)
    {
        _presenceCollection = database.GetCollection<UserPresence>("user_presence");
        
        // Create compound index
        var compoundIndex = Builders<UserPresence>.IndexKeys
            .Ascending(p => p.UserId)
            .Ascending(p => p.RoomId);
        _presenceCollection.Indexes.CreateOne(new CreateIndexModel<UserPresence>(compoundIndex,
            new CreateIndexOptions { Unique = true }));
        
        var roomIdIndex = Builders<UserPresence>.IndexKeys.Ascending(p => p.RoomId);
        _presenceCollection.Indexes.CreateOne(new CreateIndexModel<UserPresence>(roomIdIndex));
    }

    public async Task<UserPresence?> GetByUserAndRoomAsync(string userId, string roomId)
    {
        return await _presenceCollection
            .Find(p => p.UserId == userId && p.RoomId == roomId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<UserPresence>> GetByRoomIdAsync(string roomId)
    {
        return await _presenceCollection
            .Find(p => p.RoomId == roomId)
            .ToListAsync();
    }

    public async Task UpsertAsync(UserPresence presence)
    {
        var filter = Builders<UserPresence>.Filter.And(
            Builders<UserPresence>.Filter.Eq(p => p.UserId, presence.UserId),
            Builders<UserPresence>.Filter.Eq(p => p.RoomId, presence.RoomId)
        );
        
        var options = new ReplaceOptions { IsUpsert = true };
        await _presenceCollection.ReplaceOneAsync(filter, presence, options);
    }

    public async Task UpdateStatusAsync(string userId, string roomId, PresenceStatus status)
    {
        var filter = Builders<UserPresence>.Filter.And(
            Builders<UserPresence>.Filter.Eq(p => p.UserId, userId),
            Builders<UserPresence>.Filter.Eq(p => p.RoomId, roomId)
        );
        
        var update = Builders<UserPresence>.Update
            .Set(p => p.Status, status)
            .Set(p => p.LastSeen, DateTime.UtcNow);
        
        await _presenceCollection.UpdateOneAsync(filter, update);
    }

    public async Task UpdateConnectionIdAsync(string userId, string roomId, string? connectionId)
    {
        var filter = Builders<UserPresence>.Filter.And(
            Builders<UserPresence>.Filter.Eq(p => p.UserId, userId),
            Builders<UserPresence>.Filter.Eq(p => p.RoomId, roomId)
        );
        
        var update = Builders<UserPresence>.Update.Set(p => p.ConnectionId, connectionId);
        await _presenceCollection.UpdateOneAsync(filter, update);
    }
}
