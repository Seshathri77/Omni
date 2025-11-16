using OmniFlow.Core;

namespace Yath.Shared.Messages;

// ============================================================================
// USER COMMANDS
// ============================================================================

public record RegisterUser(
    string Username,
    string Email,
    string Password,
    string DisplayName
) : ICommand;

public record UpdateUserProfile(
    string UserId,
    string? DisplayName,
    string? Bio,
    string? Location,
    List<string>? TravelStyles
) : ICommand;

public record UpdateUserAvatar(
    string UserId,
    string AvatarUrl
) : ICommand;

public record FollowUser(
    string FollowerId,
    string FollowingId
) : ICommand;

public record UnfollowUser(
    string FollowerId,
    string FollowingId
) : ICommand;

// ============================================================================
// USER EVENTS
// ============================================================================

public record UserRegistered(
    string UserId,
    string Username,
    string Email,
    string DisplayName,
    DateTime RegisteredAt
) : IEvent;

public record UserProfileUpdated(
    string UserId,
    string? DisplayName,
    string? Bio,
    string? AvatarUrl,
    DateTime UpdatedAt
) : IEvent;

public record UserFollowed(
    string FollowerId,
    string FollowingId,
    DateTime FollowedAt
) : IEvent;

public record UserUnfollowed(
    string FollowerId,
    string FollowingId,
    DateTime UnfollowedAt
) : IEvent;

public record WelcomeEmailRequested(
    string UserId,
    string Email,
    string DisplayName
) : IEvent;
