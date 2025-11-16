using Yath.UserService.Models;

namespace Yath.UserService.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string userId);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task CreateAsync(User user);
    Task UpdateAsync(User user);
    Task<List<User>> SearchAsync(string query, int skip = 0, int limit = 20);
    Task<bool> ExistsAsync(string userId);
}
