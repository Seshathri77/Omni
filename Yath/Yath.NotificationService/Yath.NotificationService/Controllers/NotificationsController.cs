using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniFlow.Core;
using OmniFlow.Messaging;
using Yath.NotificationService.Models;
using Yath.NotificationService.Repositories;
using Yath.NotificationService.Services;
using Yath.Shared.DTOs;
using Yath.Shared.Messages;

namespace Yath.NotificationService.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly INotificationPreferenceRepository _preferenceRepository;
    private readonly IFcmService _fcmService;
    private readonly IMessageBus _messageBus;
    private readonly ICorrelationAccessor _correlationAccessor;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationRepository notificationRepository,
        IDeviceTokenRepository deviceTokenRepository,
        INotificationPreferenceRepository preferenceRepository,
        IFcmService fcmService,
        IMessageBus messageBus,
        ICorrelationAccessor correlationAccessor,
        ILogger<NotificationsController> logger)
    {
        _notificationRepository = notificationRepository;
        _deviceTokenRepository = deviceTokenRepository;
        _preferenceRepository = preferenceRepository;
        _fcmService = fcmService;
        _messageBus = messageBus;
        _correlationAccessor = correlationAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Get user's notifications
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<NotificationDto>>>> GetNotifications([FromQuery] int limit = 50)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<List<NotificationDto>>(false, default, "Invalid user"));

        var notifications = await _notificationRepository.GetByUserIdAsync(userId, limit);
        var dtos = notifications.Select(n => MapToDto(n)).ToList();

        return Ok(new ApiResponse<List<NotificationDto>>(true, dtos));
    }

    /// <summary>
    /// Get unread notifications count
    /// </summary>
    [HttpGet("unread/count")]
    public async Task<ActionResult<ApiResponse<int>>> GetUnreadCount()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<int>(false, default, "Invalid user"));

        var count = await _notificationRepository.GetUnreadCountAsync(userId);
        return Ok(new ApiResponse<int>(true, count));
    }

    /// <summary>
    /// Mark notification as read
    /// </summary>
    [HttpPut("{notificationId}/read")]
    public async Task<ActionResult<ApiResponse<string>>> MarkAsRead(string notificationId)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<string>(false, default, "Invalid user"));

        var notification = await _notificationRepository.GetByIdAsync(notificationId);
        if (notification == null || notification.UserId != userId)
            return NotFound(new ApiResponse<string>(false, default, "Notification not found"));

        await _notificationRepository.MarkAsReadAsync(notificationId);
        return Ok(new ApiResponse<string>(true, "Notification marked as read"));
    }

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    [HttpPut("read-all")]
    public async Task<ActionResult<ApiResponse<string>>> MarkAllAsRead()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<string>(false, default, "Invalid user"));

        await _notificationRepository.MarkAllAsReadAsync(userId);
        return Ok(new ApiResponse<string>(true, "All notifications marked as read"));
    }

    /// <summary>
    /// Delete a notification
    /// </summary>
    [HttpDelete("{notificationId}")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteNotification(string notificationId)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<string>(false, default, "Invalid user"));

        var notification = await _notificationRepository.GetByIdAsync(notificationId);
        if (notification == null || notification.UserId != userId)
            return NotFound(new ApiResponse<string>(false, default, "Notification not found"));

        await _notificationRepository.DeleteAsync(notificationId);
        return Ok(new ApiResponse<string>(true, "Notification deleted"));
    }

    /// <summary>
    /// Register device token for push notifications
    /// </summary>
    [HttpPost("devices")]
    public async Task<ActionResult<ApiResponse<DeviceTokenDto>>> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<DeviceTokenDto>(false, default, "Invalid user"));

        // Check if token already exists
        var existingToken = await _deviceTokenRepository.GetByTokenAsync(request.Token);
        if (existingToken != null)
        {
            existingToken.IsActive = true;
            existingToken.LastUsedAt = DateTime.UtcNow;
            existingToken.DeviceName = request.DeviceName;
            existingToken.DeviceModel = request.DeviceModel;
            existingToken.OsVersion = request.OsVersion;
            existingToken.AppVersion = request.AppVersion;
            
            await _deviceTokenRepository.UpdateAsync(existingToken);
            
            var dto = new DeviceTokenDto(
                existingToken.TokenId,
                existingToken.UserId,
                existingToken.Token,
                existingToken.Platform.ToString(),
                existingToken.DeviceName,
                existingToken.IsActive,
                existingToken.CreatedAt,
                existingToken.LastUsedAt
            );
            
            return Ok(new ApiResponse<DeviceTokenDto>(true, dto));
        }

        var deviceToken = new DeviceToken
        {
            UserId = userId,
            Token = request.Token,
            Platform = Enum.Parse<DevicePlatform>(request.Platform, true),
            DeviceName = request.DeviceName,
            DeviceModel = request.DeviceModel,
            OsVersion = request.OsVersion,
            AppVersion = request.AppVersion
        };

        await _deviceTokenRepository.CreateAsync(deviceToken);

        var resultDto = new DeviceTokenDto(
            deviceToken.TokenId,
            deviceToken.UserId,
            deviceToken.Token,
            deviceToken.Platform.ToString(),
            deviceToken.DeviceName,
            deviceToken.IsActive,
            deviceToken.CreatedAt,
            deviceToken.LastUsedAt
        );

        _logger.LogInformation("Registered device token for user {UserId} on {Platform}", userId, request.Platform);
        return Ok(new ApiResponse<DeviceTokenDto>(true, resultDto));
    }

    /// <summary>
    /// Get user's registered devices
    /// </summary>
    [HttpGet("devices")]
    public async Task<ActionResult<ApiResponse<List<DeviceTokenDto>>>> GetDevices()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<List<DeviceTokenDto>>(false, default, "Invalid user"));

        var tokens = await _deviceTokenRepository.GetByUserIdAsync(userId);
        var dtos = tokens.Select(t => new DeviceTokenDto(
            t.TokenId,
            t.UserId,
            t.Token,
            t.Platform.ToString(),
            t.DeviceName,
            t.IsActive,
            t.CreatedAt,
            t.LastUsedAt
        )).ToList();

        return Ok(new ApiResponse<List<DeviceTokenDto>>(true, dtos));
    }

    /// <summary>
    /// Deactivate a device token
    /// </summary>
    [HttpDelete("devices/{tokenId}")]
    public async Task<ActionResult<ApiResponse<string>>> DeactivateDevice(string tokenId)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<string>(false, default, "Invalid user"));

        var token = await _deviceTokenRepository.GetByIdAsync(tokenId);
        if (token == null || token.UserId != userId)
            return NotFound(new ApiResponse<string>(false, default, "Device token not found"));

        await _deviceTokenRepository.DeactivateAsync(tokenId);
        return Ok(new ApiResponse<string>(true, "Device token deactivated"));
    }

    /// <summary>
    /// Get notification preferences
    /// </summary>
    [HttpGet("preferences")]
    public async Task<ActionResult<ApiResponse<NotificationPreferenceDto>>> GetPreferences()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<NotificationPreferenceDto>(false, default, "Invalid user"));

        var preference = await _preferenceRepository.GetByUserIdAsync(userId);
        
        // Create default if doesn't exist
        if (preference == null)
        {
            preference = new NotificationPreference { UserId = userId };
            await _preferenceRepository.CreateAsync(preference);
        }

        var dto = MapPreferenceToDto(preference);
        return Ok(new ApiResponse<NotificationPreferenceDto>(true, dto));
    }

    /// <summary>
    /// Update notification preferences
    /// </summary>
    [HttpPut("preferences")]
    public async Task<ActionResult<ApiResponse<NotificationPreferenceDto>>> UpdatePreferences(
        [FromBody] UpdatePreferencesRequest request)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<NotificationPreferenceDto>(false, default, "Invalid user"));

        var preference = await _preferenceRepository.GetByUserIdAsync(userId);
        
        if (preference == null)
        {
            preference = new NotificationPreference { UserId = userId };
        }

        // Update preferences
        preference.EnablePushNotifications = request.EnablePushNotifications;
        preference.EnableEmailNotifications = request.EnableEmailNotifications;
        preference.EnableInAppNotifications = request.EnableInAppNotifications;
        preference.TripInvites = request.TripInvites;
        preference.TripUpdates = request.TripUpdates;
        preference.Messages = request.Messages;
        preference.Comments = request.Comments;
        preference.Likes = request.Likes;
        preference.Followers = request.Followers;
        preference.Expenses = request.Expenses;
        preference.LocationSharing = request.LocationSharing;
        preference.MediaTagging = request.MediaTagging;
        preference.TripReminders = request.TripReminders;
        preference.SystemNotifications = request.SystemNotifications;
        preference.QuietHoursEnabled = request.QuietHoursEnabled;
        preference.QuietHoursStart = request.QuietHoursStart;
        preference.QuietHoursEnd = request.QuietHoursEnd;

        if (preference.PreferenceId == null || string.IsNullOrEmpty(preference.PreferenceId))
        {
            await _preferenceRepository.CreateAsync(preference);
        }
        else
        {
            await _preferenceRepository.UpdateAsync(preference);
        }

        var dto = MapPreferenceToDto(preference);
        return Ok(new ApiResponse<NotificationPreferenceDto>(true, dto));
    }

    private NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto(
            notification.NotificationId,
            notification.Type.ToString(),
            notification.Title,
            notification.Body,
            notification.ImageUrl,
            notification.IsRead,
            notification.CreatedAt,
            notification.ActionUrl,
            notification.RelatedEntityId,
            notification.RelatedEntityType
        );
    }

    private NotificationPreferenceDto MapPreferenceToDto(NotificationPreference preference)
    {
        return new NotificationPreferenceDto(
            preference.EnablePushNotifications,
            preference.EnableEmailNotifications,
            preference.EnableInAppNotifications,
            preference.TripInvites,
            preference.TripUpdates,
            preference.Messages,
            preference.Comments,
            preference.Likes,
            preference.Followers,
            preference.Expenses,
            preference.LocationSharing,
            preference.MediaTagging,
            preference.TripReminders,
            preference.SystemNotifications,
            preference.QuietHoursEnabled,
            preference.QuietHoursStart,
            preference.QuietHoursEnd
        );
    }
}

// DTOs
public record NotificationDto(
    string NotificationId,
    string Type,
    string Title,
    string Body,
    string? ImageUrl,
    bool IsRead,
    DateTime CreatedAt,
    string? ActionUrl,
    string? RelatedEntityId,
    string? RelatedEntityType
);

public record DeviceTokenDto(
    string TokenId,
    string UserId,
    string Token,
    string Platform,
    string? DeviceName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime LastUsedAt
);

public record NotificationPreferenceDto(
    bool EnablePushNotifications,
    bool EnableEmailNotifications,
    bool EnableInAppNotifications,
    bool TripInvites,
    bool TripUpdates,
    bool Messages,
    bool Comments,
    bool Likes,
    bool Followers,
    bool Expenses,
    bool LocationSharing,
    bool MediaTagging,
    bool TripReminders,
    bool SystemNotifications,
    bool QuietHoursEnabled,
    TimeSpan? QuietHoursStart,
    TimeSpan? QuietHoursEnd
);

public record RegisterDeviceRequest(
    string Token,
    string Platform,
    string? DeviceName,
    string? DeviceModel,
    string? OsVersion,
    string? AppVersion
);

public record UpdatePreferencesRequest(
    bool EnablePushNotifications,
    bool EnableEmailNotifications,
    bool EnableInAppNotifications,
    bool TripInvites,
    bool TripUpdates,
    bool Messages,
    bool Comments,
    bool Likes,
    bool Followers,
    bool Expenses,
    bool LocationSharing,
    bool MediaTagging,
    bool TripReminders,
    bool SystemNotifications,
    bool QuietHoursEnabled,
    TimeSpan? QuietHoursStart,
    TimeSpan? QuietHoursEnd
);
