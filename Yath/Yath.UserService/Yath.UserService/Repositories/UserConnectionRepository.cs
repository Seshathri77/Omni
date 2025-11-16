using MongoDB.Driver;
using Yath.UserService.Models;

namespace Yath.UserService.Repositories;

public class UserConnectionRepository : IUserConnectionRepository
{
    private readonly IMongoCollection<UserConnection> _connections;

    public UserConnectionRepository(IMongoDatabase database)
    {
        _connections = database.GetCollection<UserConnection>("user_connections");

        // Create compound index for queries
        var followerIndex = Builders<UserConnection>.IndexKeys.Ascending(c => c.FollowerId);
        _connections.Indexes.CreateOne(new CreateIndexModel<UserConnection>(followerIndex));

        var followingIndex = Builders<UserConnection>.IndexKeys.Ascending(c => c.FollowingId);
        _connections.Indexes.CreateOne(new CreateIndexModel<UserConnection>(followingIndex));

        var compoundIndex = Builders<UserConnection>.IndexKeys
            .Ascending(c => c.FollowerId)
            .Ascending(c => c.FollowingId);
        _connections.Indexes.CreateOne(new CreateIndexModel<UserConnection>(compoundIndex,
            new CreateIndexOptions { Unique = true }));
    }

    public async Task CreateAsync(UserConnection connection)
    {
        await _connections.InsertOneAsync(connection);
    }

    public async Task DeleteAsync(string followerId, string followingId)
    {
        await _connections.DeleteOneAsync(c =>
            c.FollowerId == followerId && c.FollowingId == followingId);
    }

    public async Task<bool> ExistsAsync(string followerId, string followingId)
    {
        return await _connections.Find(c =>
            c.FollowerId == followerId && c.FollowingId == followingId).AnyAsync();
    }

    public async Task<List<UserConnection>> GetFollowersAsync(string userId, int skip = 0, int limit = 20)
    {
        return await _connections.Find(c => c.FollowingId == userId)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<UserConnection>> GetFollowingAsync(string userId, int skip = 0, int limit = 20)
    {
        return await _connections.Find(c => c.FollowerId == userId)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<int> GetFollowersCountAsync(string userId)
    {
        return (int)await _connections.CountDocumentsAsync(c => c.FollowingId == userId);
    }

    public async Task<int> GetFollowingCountAsync(string userId)
    {
        return (int)await _connections.CountDocumentsAsync(c => c.FollowerId == userId);
    }
}
