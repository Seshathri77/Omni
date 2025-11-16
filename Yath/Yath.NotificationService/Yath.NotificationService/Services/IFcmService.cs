using Yath.NotificationService.Models;

namespace Yath.NotificationService.Services;

public interface IFcmService
{
    Task<string?> SendNotificationAsync(DeviceToken deviceToken, Models.Notification notification);
    Task<Dictionary<string, string?>> SendToMultipleDevicesAsync(List<DeviceToken> deviceTokens, Models.Notification notification);
    Task<string?> SendDataMessageAsync(DeviceToken deviceToken, Dictionary<string, string> data);
    bool IsInitialized { get; }
}
