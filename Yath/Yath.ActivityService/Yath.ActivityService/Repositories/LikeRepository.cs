using MongoDB.Driver;
using Yath.ActivityService.Models;

namespace Yath.ActivityService.Repositories;

public class LikeRepository : ILikeRepository
{
    private readonly IMongoCollection<Like> _likes;

    public LikeRepository(IMongoDatabase database)
    {
        _likes = database.GetCollection<Like>("likes");

        // Note: Indexes should be created asynchronously after startup or via migrations
        // Creating indexes here can cause timeout issues with MongoDB Atlas in containerized environments
    }

    public async Task<Like?> GetAsync(string postId, string userId)
    {
        return await _likes.Find(l => l.PostId == postId && l.UserId == userId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Like>> GetByPostIdAsync(string postId, int skip = 0, int limit = 100)
    {
        return await _likes.Find(l => l.PostId == postId)
            .SortByDescending(l => l.LikedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<bool> HasLikedAsync(string postId, string userId)
    {
        return await _likes.Find(l => l.PostId == postId && l.UserId == userId).AnyAsync();
    }

    public async Task CreateAsync(Like like)
    {
        await _likes.InsertOneAsync(like);
    }

    public async Task DeleteAsync(string postId, string userId)
    {
        await _likes.DeleteOneAsync(l => l.PostId == postId && l.UserId == userId);
    }

    public async Task DeleteByPostIdAsync(string postId)
    {
        await _likes.DeleteManyAsync(l => l.PostId == postId);
    }
}
