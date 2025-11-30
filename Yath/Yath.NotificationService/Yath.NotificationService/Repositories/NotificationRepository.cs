using MongoDB.Driver;
using Yath.NotificationService.Models;

namespace Yath.NotificationService.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly IMongoCollection<Notification> _collection;

    public NotificationRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<Notification>("notifications");
        // Index creation removed - should be done via migration or startup task
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<Notification>.IndexKeys;

        // Index on notificationId (unique)
        _collection.Indexes.CreateOne(
            new CreateIndexModel<Notification>(
                indexKeys.Ascending(n => n.NotificationId),
                new CreateIndexOptions { Unique = true }
            )
        );

        // Compound index on userId + createdAt (for user's notification feed)
        _collection.Indexes.CreateOne(
            new CreateIndexModel<Notification>(
                indexKeys.Ascending(n => n.UserId).Descending(n => n.CreatedAt)
            )
        );

        // Compound index on userId + isRead (for unread count)
        _collection.Indexes.CreateOne(
            new CreateIndexModel<Notification>(
                indexKeys.Ascending(n => n.UserId).Ascending(n => n.IsRead)
            )
        );

        // Compound index on userId + type
        _collection.Indexes.CreateOne(
            new CreateIndexModel<Notification>(
                indexKeys.Ascending(n => n.UserId).Ascending(n => n.Type)
            )
        );

        // TTL index on expiresAt
        _collection.Indexes.CreateOne(
            new CreateIndexModel<Notification>(
                indexKeys.Ascending(n => n.ExpiresAt),
                new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }
            )
        );
    }

    public async Task<Notification?> GetByIdAsync(string notificationId)
    {
        return await _collection.Find(n => n.NotificationId == notificationId).FirstOrDefaultAsync();
    }

    public async Task<List<Notification>> GetByUserIdAsync(string userId, int limit = 50)
    {
        return await _collection
            .Find(n => n.UserId == userId)
            .SortByDescending(n => n.CreatedAt)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<Notification>> GetUnreadByUserIdAsync(string userId)
    {
        return await _collection
            .Find(n => n.UserId == userId && !n.IsRead)
            .SortByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return (int)await _collection.CountDocumentsAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<List<Notification>> GetByTypeAsync(string userId, NotificationType type, int limit = 20)
    {
        return await _collection
            .Find(n => n.UserId == userId && n.Type == type)
            .SortByDescending(n => n.CreatedAt)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task CreateAsync(Notification notification)
    {
        await _collection.InsertOneAsync(notification);
    }

    public async Task UpdateAsync(Notification notification)
    {
        await _collection.ReplaceOneAsync(n => n.NotificationId == notification.NotificationId, notification);
    }

    public async Task MarkAsReadAsync(string notificationId)
    {
        var update = Builders<Notification>.Update
            .Set(n => n.IsRead, true)
            .Set(n => n.ReadAt, DateTime.UtcNow);

        await _collection.UpdateOneAsync(n => n.NotificationId == notificationId, update);
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var update = Builders<Notification>.Update
            .Set(n => n.IsRead, true)
            .Set(n => n.ReadAt, DateTime.UtcNow);

        await _collection.UpdateManyAsync(n => n.UserId == userId && !n.IsRead, update);
    }

    public async Task DeleteAsync(string notificationId)
    {
        await _collection.DeleteOneAsync(n => n.NotificationId == notificationId);
    }

    public async Task DeleteExpiredAsync()
    {
        var now = DateTime.UtcNow;
        await _collection.DeleteManyAsync(n => n.ExpiresAt != null && n.ExpiresAt < now);
    }
}
