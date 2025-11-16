using Yath.NotificationService.Models;

namespace Yath.NotificationService.Repositories;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(string notificationId);
    Task<List<Notification>> GetByUserIdAsync(string userId, int limit = 50);
    Task<List<Notification>> GetUnreadByUserIdAsync(string userId);
    Task<int> GetUnreadCountAsync(string userId);
    Task<List<Notification>> GetByTypeAsync(string userId, NotificationType type, int limit = 20);
    Task CreateAsync(Notification notification);
    Task UpdateAsync(Notification notification);
    Task MarkAsReadAsync(string notificationId);
    Task MarkAllAsReadAsync(string userId);
    Task DeleteAsync(string notificationId);
    Task DeleteExpiredAsync();
}
