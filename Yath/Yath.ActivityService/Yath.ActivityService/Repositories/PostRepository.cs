using MongoDB.Driver;
using Yath.ActivityService.Models;

namespace Yath.ActivityService.Repositories;

public class PostRepository : IPostRepository
{
    private readonly IMongoCollection<Post> _posts;

    public PostRepository(IMongoDatabase database)
    {
        _posts = database.GetCollection<Post>("posts");

        // Note: Indexes should be created asynchronously after startup or via migrations
        // Creating indexes here can cause timeout issues with MongoDB Atlas in containerized environments
    }

    public async Task<Post?> GetByIdAsync(string postId)
    {
        return await _posts.Find(p => p.PostId == postId).FirstOrDefaultAsync();
    }

    public async Task<List<Post>> GetByUserIdAsync(string userId, int skip = 0, int limit = 20)
    {
        return await _posts.Find(p => p.UserId == userId)
            .SortByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<Post>> GetFeedAsync(List<string> userIds, int skip = 0, int limit = 20)
    {
        var filter = Builders<Post>.Filter.And(
            Builders<Post>.Filter.In(p => p.UserId, userIds),
            Builders<Post>.Filter.Eq(p => p.Visibility, PostVisibility.Public)
        );

        return await _posts.Find(filter)
            .SortByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<Post>> GetByTripIdAsync(string tripId, int skip = 0, int limit = 20)
    {
        return await _posts.Find(p => p.TripId == tripId)
            .SortByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<Post>> SearchByTagsAsync(List<string> tags, int skip = 0, int limit = 20)
    {
        var filter = Builders<Post>.Filter.AnyIn(p => p.Tags, tags);
        
        return await _posts.Find(filter)
            .SortByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task CreateAsync(Post post)
    {
        await _posts.InsertOneAsync(post);
    }

    public async Task UpdateAsync(Post post)
    {
        post.UpdatedAt = DateTime.UtcNow;
        await _posts.ReplaceOneAsync(p => p.PostId == post.PostId, post);
    }

    public async Task DeleteAsync(string postId)
    {
        await _posts.DeleteOneAsync(p => p.PostId == postId);
    }

    public async Task IncrementLikesCountAsync(string postId)
    {
        var update = Builders<Post>.Update.Inc(p => p.LikesCount, 1);
        await _posts.UpdateOneAsync(p => p.PostId == postId, update);
    }

    public async Task DecrementLikesCountAsync(string postId)
    {
        var update = Builders<Post>.Update.Inc(p => p.LikesCount, -1);
        await _posts.UpdateOneAsync(p => p.PostId == postId, update);
    }

    public async Task IncrementCommentsCountAsync(string postId)
    {
        var update = Builders<Post>.Update.Inc(p => p.CommentsCount, 1);
        await _posts.UpdateOneAsync(p => p.PostId == postId, update);
    }

    public async Task DecrementCommentsCountAsync(string postId)
    {
        var update = Builders<Post>.Update.Inc(p => p.CommentsCount, -1);
        await _posts.UpdateOneAsync(p => p.PostId == postId, update);
    }
}
