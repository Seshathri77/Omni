using Yath.NotificationService.Models;

namespace Yath.NotificationService.Repositories;

public interface IDeviceTokenRepository
{
    Task<DeviceToken?> GetByIdAsync(string tokenId);
    Task<DeviceToken?> GetByTokenAsync(string token);
    Task<List<DeviceToken>> GetByUserIdAsync(string userId);
    Task<List<DeviceToken>> GetActiveByUserIdAsync(string userId);
    Task CreateAsync(DeviceToken deviceToken);
    Task UpdateAsync(DeviceToken deviceToken);
    Task DeactivateAsync(string tokenId);
    Task DeactivateByTokenAsync(string token);
    Task DeleteAsync(string tokenId);
}
