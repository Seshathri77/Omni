using OmniFlow.Core;

namespace Yath.Shared.Messages;

// ============================================================================
// ACTIVITY COMMANDS
// ============================================================================

public record CreateActivity(
    string UserId,
    string? TripId,
    string Caption,
    LocationInfo? Location,
    List<string> Tags,
    List<string> MediaIds,
    string Visibility // "public", "followers", "private"
) : ICommand;

public record UpdateActivity(
    string ActivityId,
    string? Caption,
    List<string>? Tags
) : ICommand;

public record DeleteActivity(
    string ActivityId,
    string UserId
) : ICommand;

public record LikeActivity(
    string ActivityId,
    string UserId
) : ICommand;

public record UnlikeActivity(
    string ActivityId,
    string UserId
) : ICommand;

public record AddComment(
    string ActivityId,
    string UserId,
    string Text
) : ICommand;

public record DeleteComment(
    string CommentId,
    string UserId
) : ICommand;

// ============================================================================
// ACTIVITY EVENTS
// ============================================================================

public record ActivityCreated(
    string ActivityId,
    string UserId,
    string? TripId,
    string Caption,
    LocationInfo? Location,
    List<string> Tags,
    List<string> MediaUrls,
    string Visibility,
    DateTime CreatedAt
) : IEvent;

public record ActivityUpdated(
    string ActivityId,
    string? Caption,
    List<string>? Tags,
    DateTime UpdatedAt
) : IEvent;

public record ActivityDeleted(
    string ActivityId,
    string UserId,
    DateTime DeletedAt
) : IEvent;

public record ActivityLiked(
    string ActivityId,
    string LikedBy,
    DateTime LikedAt
) : IEvent;

public record ActivityUnliked(
    string ActivityId,
    string UnlikedBy,
    DateTime UnlikedAt
) : IEvent;

public record CommentAdded(
    string CommentId,
    string ActivityId,
    string UserId,
    string Text,
    DateTime CreatedAt
) : IEvent;

public record CommentDeleted(
    string CommentId,
    string ActivityId,
    DateTime DeletedAt
) : IEvent;

// ============================================================================
// SUPPORTING TYPES
// ============================================================================

public record LocationInfo(
    string Name,
    double Latitude,
    double Longitude,
    string? PlaceId
);
