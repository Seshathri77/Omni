using Yath.ActivityService.Models;

namespace Yath.ActivityService.Repositories;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(string postId);
    Task<List<Post>> GetByUserIdAsync(string userId, int skip = 0, int limit = 20);
    Task<List<Post>> GetFeedAsync(List<string> userIds, int skip = 0, int limit = 20);
    Task<List<Post>> GetByTripIdAsync(string tripId, int skip = 0, int limit = 20);
    Task<List<Post>> SearchByTagsAsync(List<string> tags, int skip = 0, int limit = 20);
    Task CreateAsync(Post post);
    Task UpdateAsync(Post post);
    Task DeleteAsync(string postId);
    Task IncrementLikesCountAsync(string postId);
    Task DecrementLikesCountAsync(string postId);
    Task IncrementCommentsCountAsync(string postId);
    Task DecrementCommentsCountAsync(string postId);
}
