using MongoDB.Driver;
using Yath.NotificationService.Models;

namespace Yath.NotificationService.Repositories;

public class DeviceTokenRepository : IDeviceTokenRepository
{
    private readonly IMongoCollection<DeviceToken> _collection;

    public DeviceTokenRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<DeviceToken>("device_tokens");
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<DeviceToken>.IndexKeys;

        // Index on tokenId (unique)
        _collection.Indexes.CreateOne(
            new CreateIndexModel<DeviceToken>(
                indexKeys.Ascending(t => t.TokenId),
                new CreateIndexOptions { Unique = true }
            )
        );

        // Index on token (unique)
        _collection.Indexes.CreateOne(
            new CreateIndexModel<DeviceToken>(
                indexKeys.Ascending(t => t.Token),
                new CreateIndexOptions { Unique = true }
            )
        );

        // Index on userId
        _collection.Indexes.CreateOne(
            new CreateIndexModel<DeviceToken>(
                indexKeys.Ascending(t => t.UserId)
            )
        );

        // Compound index on userId + isActive
        _collection.Indexes.CreateOne(
            new CreateIndexModel<DeviceToken>(
                indexKeys.Ascending(t => t.UserId).Ascending(t => t.IsActive)
            )
        );
    }

    public async Task<DeviceToken?> GetByIdAsync(string tokenId)
    {
        return await _collection.Find(t => t.TokenId == tokenId).FirstOrDefaultAsync();
    }

    public async Task<DeviceToken?> GetByTokenAsync(string token)
    {
        return await _collection.Find(t => t.Token == token).FirstOrDefaultAsync();
    }

    public async Task<List<DeviceToken>> GetByUserIdAsync(string userId)
    {
        return await _collection
            .Find(t => t.UserId == userId)
            .SortByDescending(t => t.LastUsedAt)
            .ToListAsync();
    }

    public async Task<List<DeviceToken>> GetActiveByUserIdAsync(string userId)
    {
        return await _collection
            .Find(t => t.UserId == userId && t.IsActive)
            .ToListAsync();
    }

    public async Task CreateAsync(DeviceToken deviceToken)
    {
        await _collection.InsertOneAsync(deviceToken);
    }

    public async Task UpdateAsync(DeviceToken deviceToken)
    {
        deviceToken.LastUsedAt = DateTime.UtcNow;
        await _collection.ReplaceOneAsync(t => t.TokenId == deviceToken.TokenId, deviceToken);
    }

    public async Task DeactivateAsync(string tokenId)
    {
        var update = Builders<DeviceToken>.Update.Set(t => t.IsActive, false);
        await _collection.UpdateOneAsync(t => t.TokenId == tokenId, update);
    }

    public async Task DeactivateByTokenAsync(string token)
    {
        var update = Builders<DeviceToken>.Update.Set(t => t.IsActive, false);
        await _collection.UpdateOneAsync(t => t.Token == token, update);
    }

    public async Task DeleteAsync(string tokenId)
    {
        await _collection.DeleteOneAsync(t => t.TokenId == tokenId);
    }
}
