using MongoDB.Driver;
using Yath.NotificationService.Models;

namespace Yath.NotificationService.Repositories;

public class NotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly IMongoCollection<NotificationPreference> _collection;

    public NotificationPreferenceRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<NotificationPreference>("notification_preferences");
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<NotificationPreference>.IndexKeys;

        // Index on userId (unique)
        _collection.Indexes.CreateOne(
            new CreateIndexModel<NotificationPreference>(
                indexKeys.Ascending(p => p.UserId),
                new CreateIndexOptions { Unique = true }
            )
        );
    }

    public async Task<NotificationPreference?> GetByUserIdAsync(string userId)
    {
        return await _collection.Find(p => p.UserId == userId).FirstOrDefaultAsync();
    }

    public async Task CreateAsync(NotificationPreference preference)
    {
        await _collection.InsertOneAsync(preference);
    }

    public async Task UpdateAsync(NotificationPreference preference)
    {
        preference.UpdatedAt = DateTime.UtcNow;
        await _collection.ReplaceOneAsync(p => p.UserId == preference.UserId, preference);
    }

    public async Task DeleteAsync(string userId)
    {
        await _collection.DeleteOneAsync(p => p.UserId == userId);
    }
}
