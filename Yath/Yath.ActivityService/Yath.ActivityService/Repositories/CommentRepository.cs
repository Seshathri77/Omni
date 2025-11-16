using MongoDB.Driver;
using Yath.ActivityService.Models;

namespace Yath.ActivityService.Repositories;

public class CommentRepository : ICommentRepository
{
    private readonly IMongoCollection<Comment> _comments;

    public CommentRepository(IMongoDatabase database)
    {
        _comments = database.GetCollection<Comment>("comments");

        // Create indexes
        var commentIdIndex = Builders<Comment>.IndexKeys.Ascending(c => c.CommentId);
        _comments.Indexes.CreateOne(new CreateIndexModel<Comment>(commentIdIndex,
            new CreateIndexOptions { Unique = true }));

        var postIdIndex = Builders<Comment>.IndexKeys.Ascending(c => c.PostId);
        _comments.Indexes.CreateOne(new CreateIndexModel<Comment>(postIdIndex));

        var parentCommentIndex = Builders<Comment>.IndexKeys.Ascending(c => c.ParentCommentId);
        _comments.Indexes.CreateOne(new CreateIndexModel<Comment>(parentCommentIndex));
    }

    public async Task<Comment?> GetByIdAsync(string commentId)
    {
        return await _comments.Find(c => c.CommentId == commentId).FirstOrDefaultAsync();
    }

    public async Task<List<Comment>> GetByPostIdAsync(string postId, int skip = 0, int limit = 50)
    {
        return await _comments.Find(c => c.PostId == postId && c.ParentCommentId == null)
            .SortBy(c => c.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<Comment>> GetRepliesAsync(string commentId, int skip = 0, int limit = 20)
    {
        return await _comments.Find(c => c.ParentCommentId == commentId)
            .SortBy(c => c.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task CreateAsync(Comment comment)
    {
        await _comments.InsertOneAsync(comment);
    }

    public async Task DeleteAsync(string commentId)
    {
        await _comments.DeleteOneAsync(c => c.CommentId == commentId);
    }

    public async Task DeleteByPostIdAsync(string postId)
    {
        await _comments.DeleteManyAsync(c => c.PostId == postId);
    }
}
