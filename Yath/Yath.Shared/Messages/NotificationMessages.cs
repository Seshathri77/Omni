using OmniFlow.Core;

namespace Yath.Shared.Messages;

// ============================================================================
// NOTIFICATION COMMANDS
// ============================================================================

public record SendNotification(
    string UserId,
    string Type, // "like", "comment", "follow", "trip_invite", "expense_added", "message"
    string Title,
    string Body,
    Dictionary<string, string> Payload
) : ICommand;

public record RegisterDeviceToken(
    string UserId,
    string DeviceToken,
    string Platform // "ios", "android"
) : ICommand;

public record MarkNotificationAsRead(
    string NotificationId,
    string UserId
) : ICommand;

// ============================================================================
// NOTIFICATION EVENTS
// ============================================================================

public record NotificationSent(
    string NotificationId,
    string UserId,
    string Type,
    string Title,
    string Body,
    DateTime SentAt
) : IEvent;

public record DeviceTokenRegistered(
    string UserId,
    string DeviceToken,
    string Platform,
    DateTime RegisteredAt
) : IEvent;

public record NotificationRead(
    string NotificationId,
    string UserId,
    DateTime ReadAt
) : IEvent;
