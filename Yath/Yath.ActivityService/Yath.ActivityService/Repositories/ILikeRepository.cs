using Yath.ActivityService.Models;

namespace Yath.ActivityService.Repositories;

public interface ILikeRepository
{
    Task<Like?> GetAsync(string postId, string userId);
    Task<List<Like>> GetByPostIdAsync(string postId, int skip = 0, int limit = 100);
    Task<bool> HasLikedAsync(string postId, string userId);
    Task CreateAsync(Like like);
    Task DeleteAsync(string postId, string userId);
    Task DeleteByPostIdAsync(string postId);
}
