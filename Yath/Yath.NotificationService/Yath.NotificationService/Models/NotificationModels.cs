using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yath.NotificationService.Models;

/// <summary>
/// Represents a notification sent to a user
/// </summary>
public class Notification
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string NotificationId { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("type")]
    public NotificationType Type { get; set; }

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("body")]
    public string Body { get; set; } = string.Empty;

    [BsonElement("payload")]
    public Dictionary<string, string> Payload { get; set; } = new();

    [BsonElement("imageUrl")]
    public string? ImageUrl { get; set; }

    [BsonElement("isRead")]
    public bool IsRead { get; set; }

    [BsonElement("readAt")]
    public DateTime? ReadAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    [BsonElement("priority")]
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

    [BsonElement("actionUrl")]
    public string? ActionUrl { get; set; }

    [BsonElement("relatedEntityId")]
    public string? RelatedEntityId { get; set; } // tripId, postId, messageId, etc.

    [BsonElement("relatedEntityType")]
    public string? RelatedEntityType { get; set; } // "trip", "post", "message", etc.
}

/// <summary>
/// Types of notifications
/// </summary>
public enum NotificationType
{
    TripInvite = 0,
    TripUpdate = 1,
    NewMessage = 2,
    NewComment = 3,
    NewLike = 4,
    NewFollower = 5,
    ExpenseAdded = 6,
    ExpenseSettlement = 7,
    LocationShared = 8,
    MediaTagged = 9,
    TripReminder = 10,
    System = 11
}

/// <summary>
/// Priority levels for notifications
/// </summary>
public enum NotificationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

/// <summary>
/// Device token for push notifications
/// </summary>
public class DeviceToken
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string TokenId { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("token")]
    public string Token { get; set; } = string.Empty;

    [BsonElement("platform")]
    public DevicePlatform Platform { get; set; }

    [BsonElement("deviceName")]
    public string? DeviceName { get; set; }

    [BsonElement("deviceModel")]
    public string? DeviceModel { get; set; }

    [BsonElement("osVersion")]
    public string? OsVersion { get; set; }

    [BsonElement("appVersion")]
    public string? AppVersion { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("lastUsedAt")]
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Device platforms
/// </summary>
public enum DevicePlatform
{
    iOS = 0,
    Android = 1,
    Web = 2
}

/// <summary>
/// User notification preferences
/// </summary>
public class NotificationPreference
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string PreferenceId { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("enablePushNotifications")]
    public bool EnablePushNotifications { get; set; } = true;

    [BsonElement("enableEmailNotifications")]
    public bool EnableEmailNotifications { get; set; } = true;

    [BsonElement("enableInAppNotifications")]
    public bool EnableInAppNotifications { get; set; } = true;

    [BsonElement("tripInvites")]
    public bool TripInvites { get; set; } = true;

    [BsonElement("tripUpdates")]
    public bool TripUpdates { get; set; } = true;

    [BsonElement("messages")]
    public bool Messages { get; set; } = true;

    [BsonElement("comments")]
    public bool Comments { get; set; } = true;

    [BsonElement("likes")]
    public bool Likes { get; set; } = true;

    [BsonElement("followers")]
    public bool Followers { get; set; } = true;

    [BsonElement("expenses")]
    public bool Expenses { get; set; } = true;

    [BsonElement("locationSharing")]
    public bool LocationSharing { get; set; } = true;

    [BsonElement("mediaTagging")]
    public bool MediaTagging { get; set; } = true;

    [BsonElement("tripReminders")]
    public bool TripReminders { get; set; } = true;

    [BsonElement("systemNotifications")]
    public bool SystemNotifications { get; set; } = true;

    [BsonElement("quietHoursEnabled")]
    public bool QuietHoursEnabled { get; set; }

    [BsonElement("quietHoursStart")]
    public TimeSpan? QuietHoursStart { get; set; } // e.g., 22:00

    [BsonElement("quietHoursEnd")]
    public TimeSpan? QuietHoursEnd { get; set; } // e.g., 08:00

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Notification delivery tracking
/// </summary>
public class NotificationDelivery
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string DeliveryId { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("notificationId")]
    public string NotificationId { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("tokenId")]
    public string TokenId { get; set; } = string.Empty;

    [BsonElement("platform")]
    public DevicePlatform Platform { get; set; }

    [BsonElement("status")]
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    [BsonElement("sentAt")]
    public DateTime? SentAt { get; set; }

    [BsonElement("deliveredAt")]
    public DateTime? DeliveredAt { get; set; }

    [BsonElement("failedAt")]
    public DateTime? FailedAt { get; set; }

    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }

    [BsonElement("fcmMessageId")]
    public string? FcmMessageId { get; set; }

    [BsonElement("retryCount")]
    public int RetryCount { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Delivery status
/// </summary>
public enum DeliveryStatus
{
    Pending = 0,
    Sent = 1,
    Delivered = 2,
    Failed = 3,
    Expired = 4
}
