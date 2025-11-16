using Yath.UserService.Models;

namespace Yath.UserService.Repositories;

public interface IUserConnectionRepository
{
    Task CreateAsync(UserConnection connection);
    Task DeleteAsync(string followerId, string followingId);
    Task<bool> ExistsAsync(string followerId, string followingId);
    Task<List<UserConnection>> GetFollowersAsync(string userId, int skip = 0, int limit = 20);
    Task<List<UserConnection>> GetFollowingAsync(string userId, int skip = 0, int limit = 20);
    Task<int> GetFollowersCountAsync(string userId);
    Task<int> GetFollowingCountAsync(string userId);
}
