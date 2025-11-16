using Yath.NotificationService.Models;

namespace Yath.NotificationService.Repositories;

public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> GetByUserIdAsync(string userId);
    Task CreateAsync(NotificationPreference preference);
    Task UpdateAsync(NotificationPreference preference);
    Task DeleteAsync(string userId);
}
