using MongoDB.Driver;
using Yath.MediaService.Models;

namespace Yath.MediaService.Repositories;

public class MediaRepository : IMediaRepository
{
    private readonly IMongoCollection<Media> _mediaCollection;

    public MediaRepository(IMongoDatabase database)
    {
        _mediaCollection = database.GetCollection<Media>("media");
        
        // Create indexes
        var mediaIdIndex = Builders<Media>.IndexKeys.Ascending(m => m.MediaId);
        _mediaCollection.Indexes.CreateOne(new CreateIndexModel<Media>(mediaIdIndex, 
            new CreateIndexOptions { Unique = true }));
        
        var userIdIndex = Builders<Media>.IndexKeys.Ascending(m => m.UserId);
        _mediaCollection.Indexes.CreateOne(new CreateIndexModel<Media>(userIdIndex));
        
        var tripIdIndex = Builders<Media>.IndexKeys.Ascending(m => m.TripId);
        _mediaCollection.Indexes.CreateOne(new CreateIndexModel<Media>(tripIdIndex));
        
        var activityIdIndex = Builders<Media>.IndexKeys.Ascending(m => m.ActivityId);
        _mediaCollection.Indexes.CreateOne(new CreateIndexModel<Media>(activityIdIndex));
        
        var createdAtIndex = Builders<Media>.IndexKeys.Descending(m => m.CreatedAt);
        _mediaCollection.Indexes.CreateOne(new CreateIndexModel<Media>(createdAtIndex));
    }

    public async Task<Media?> GetByIdAsync(string mediaId)
    {
        return await _mediaCollection.Find(m => m.MediaId == mediaId).FirstOrDefaultAsync();
    }

    public async Task<List<Media>> GetByUserIdAsync(string userId, int skip = 0, int limit = 50)
    {
        return await _mediaCollection
            .Find(m => m.UserId == userId && m.UploadStatus == UploadStatus.Completed)
            .SortByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<Media>> GetByTripIdAsync(string tripId, int skip = 0, int limit = 50)
    {
        return await _mediaCollection
            .Find(m => m.TripId == tripId && m.UploadStatus == UploadStatus.Completed)
            .SortByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<Media>> GetByActivityIdAsync(string activityId)
    {
        return await _mediaCollection
            .Find(m => m.ActivityId == activityId && m.UploadStatus == UploadStatus.Completed)
            .SortByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<Media> CreateAsync(Media media)
    {
        await _mediaCollection.InsertOneAsync(media);
        return media;
    }

    public async Task UpdateAsync(Media media)
    {
        media.UpdatedAt = DateTime.UtcNow;
        await _mediaCollection.ReplaceOneAsync(m => m.MediaId == media.MediaId, media);
    }

    public async Task DeleteAsync(string mediaId)
    {
        await _mediaCollection.DeleteOneAsync(m => m.MediaId == mediaId);
    }
}
