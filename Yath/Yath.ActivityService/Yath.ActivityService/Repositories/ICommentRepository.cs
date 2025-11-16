using Yath.ActivityService.Models;

namespace Yath.ActivityService.Repositories;

public interface ICommentRepository
{
    Task<Comment?> GetByIdAsync(string commentId);
    Task<List<Comment>> GetByPostIdAsync(string postId, int skip = 0, int limit = 50);
    Task<List<Comment>> GetRepliesAsync(string commentId, int skip = 0, int limit = 20);
    Task CreateAsync(Comment comment);
    Task DeleteAsync(string commentId);
    Task DeleteByPostIdAsync(string postId);
}
