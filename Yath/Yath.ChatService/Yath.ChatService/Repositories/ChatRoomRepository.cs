using MongoDB.Driver;
using Yath.ChatService.Models;

namespace Yath.ChatService.Repositories;

public class ChatRoomRepository : IChatRoomRepository
{
    private readonly IMongoCollection<ChatRoom> _roomsCollection;

    public ChatRoomRepository(IMongoDatabase database)
    {
        _roomsCollection = database.GetCollection<ChatRoom>("chat_rooms");
        
        // Create indexes
        var roomIdIndex = Builders<ChatRoom>.IndexKeys.Ascending(r => r.RoomId);
        _roomsCollection.Indexes.CreateOne(new CreateIndexModel<ChatRoom>(roomIdIndex, 
            new CreateIndexOptions { Unique = true }));
        
        var tripIdIndex = Builders<ChatRoom>.IndexKeys.Ascending(r => r.TripId);
        _roomsCollection.Indexes.CreateOne(new CreateIndexModel<ChatRoom>(tripIdIndex,
            new CreateIndexOptions { Unique = true }));
        
        var participantsIndex = Builders<ChatRoom>.IndexKeys.Ascending(r => r.ParticipantIds);
        _roomsCollection.Indexes.CreateOne(new CreateIndexModel<ChatRoom>(participantsIndex));
    }

    public async Task<ChatRoom?> GetByIdAsync(string roomId)
    {
        return await _roomsCollection.Find(r => r.RoomId == roomId).FirstOrDefaultAsync();
    }

    public async Task<ChatRoom?> GetByTripIdAsync(string tripId)
    {
        return await _roomsCollection.Find(r => r.TripId == tripId).FirstOrDefaultAsync();
    }

    public async Task<List<ChatRoom>> GetByUserIdAsync(string userId)
    {
        return await _roomsCollection
            .Find(r => r.ParticipantIds.Contains(userId))
            .SortByDescending(r => r.UpdatedAt)
            .ToListAsync();
    }

    public async Task<ChatRoom> CreateAsync(ChatRoom room)
    {
        await _roomsCollection.InsertOneAsync(room);
        return room;
    }

    public async Task UpdateAsync(ChatRoom room)
    {
        room.UpdatedAt = DateTime.UtcNow;
        await _roomsCollection.ReplaceOneAsync(r => r.RoomId == room.RoomId, room);
    }

    public async Task AddParticipantAsync(string roomId, string userId)
    {
        var update = Builders<ChatRoom>.Update
            .AddToSet(r => r.ParticipantIds, userId)
            .Set(r => r.UpdatedAt, DateTime.UtcNow);
        
        await _roomsCollection.UpdateOneAsync(r => r.RoomId == roomId, update);
    }

    public async Task RemoveParticipantAsync(string roomId, string userId)
    {
        var update = Builders<ChatRoom>.Update
            .Pull(r => r.ParticipantIds, userId)
            .Set(r => r.UpdatedAt, DateTime.UtcNow);
        
        await _roomsCollection.UpdateOneAsync(r => r.RoomId == roomId, update);
    }
}
