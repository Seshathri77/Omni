using OmniFlow.Core;

namespace Yath.Shared.Messages;

// ============================================================================
// CHAT COMMANDS
// ============================================================================

public record CreateChatRoom(
    string TripId,
    List<string> ParticipantIds
) : ICommand;

public record SendMessage(
    string RoomId,
    string UserId,
    string? Text,
    string? MediaUrl,
    LocationInfo? Location
) : ICommand;

public record MarkMessageAsRead(
    string MessageId,
    string UserId
) : ICommand;

// ============================================================================
// CHAT EVENTS
// ============================================================================

public record ChatRoomCreated(
    string RoomId,
    string TripId,
    List<string> ParticipantIds,
    DateTime CreatedAt
) : IEvent;

public record MessageSent(
    string MessageId,
    string RoomId,
    string UserId,
    string? Text,
    string? MediaUrl,
    LocationInfo? Location,
    DateTime Timestamp
) : IEvent;

public record MessageRead(
    string MessageId,
    string UserId,
    DateTime ReadAt
) : IEvent;
