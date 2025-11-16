using MongoDB.Driver;
using Yath.UserService.Models;

namespace Yath.UserService.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;

    public UserRepository(IMongoDatabase database)
    {
        _users = database.GetCollection<User>("users");

        // Create indexes
        var userIdIndex = Builders<User>.IndexKeys.Ascending(u => u.UserId);
        _users.Indexes.CreateOne(new CreateIndexModel<User>(userIdIndex,
            new CreateIndexOptions { Unique = true }));

        var usernameIndex = Builders<User>.IndexKeys.Ascending(u => u.Username);
        _users.Indexes.CreateOne(new CreateIndexModel<User>(usernameIndex,
            new CreateIndexOptions { Unique = true }));

        var emailIndex = Builders<User>.IndexKeys.Ascending(u => u.Email);
        _users.Indexes.CreateOne(new CreateIndexModel<User>(emailIndex,
            new CreateIndexOptions { Unique = true }));
    }

    public async Task<User?> GetByIdAsync(string userId)
    {
        return await _users.Find(u => u.UserId == userId).FirstOrDefaultAsync();
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _users.Find(u => u.Username == username).FirstOrDefaultAsync();
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
    }

    public async Task CreateAsync(User user)
    {
        await _users.InsertOneAsync(user);
    }

    public async Task UpdateAsync(User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        await _users.ReplaceOneAsync(u => u.UserId == user.UserId, user);
    }

    public async Task<List<User>> SearchAsync(string query, int skip = 0, int limit = 20)
    {
        var filter = Builders<User>.Filter.Or(
            Builders<User>.Filter.Regex(u => u.Username, new MongoDB.Bson.BsonRegularExpression(query, "i")),
            Builders<User>.Filter.Regex(u => u.Profile.DisplayName, new MongoDB.Bson.BsonRegularExpression(query, "i"))
        );

        return await _users.Find(filter)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(string userId)
    {
        return await _users.Find(u => u.UserId == userId).AnyAsync();
    }
}
