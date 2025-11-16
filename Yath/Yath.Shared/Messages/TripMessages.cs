using OmniFlow.Core;

namespace Yath.Shared.Messages;

// ============================================================================
// TRIP COMMANDS
// ============================================================================

public record CreateTrip(
    string CreatorId,
    string Title,
    string Description,
    DateTime StartDate,
    DateTime EndDate,
    List<string> Destinations,
    string Visibility // "public", "private"
) : ICommand;

public record UpdateTrip(
    string TripId,
    string? Title,
    string? Description,
    DateTime? StartDate,
    DateTime? EndDate
) : ICommand;

public record AddTripParticipant(
    string TripId,
    string UserId,
    string Role // "owner", "editor", "viewer"
) : ICommand;

public record RemoveTripParticipant(
    string TripId,
    string UserId
) : ICommand;

public record UpdateTripStatus(
    string TripId,
    string Status // "planning", "ongoing", "completed", "cancelled"
) : ICommand;

public record AddItineraryDay(
    string TripId,
    int Day,
    DateTime Date,
    List<ItineraryActivity> Activities
) : ICommand;

// ============================================================================
// TRIP EVENTS
// ============================================================================

public record TripCreated(
    string TripId,
    string CreatorId,
    string Title,
    string Description,
    DateTime StartDate,
    DateTime EndDate,
    List<string> Destinations,
    DateTime CreatedAt
) : IEvent;

public record TripUpdated(
    string TripId,
    DateTime UpdatedAt
) : IEvent;

public record TripParticipantAdded(
    string TripId,
    string UserId,
    string Role,
    DateTime AddedAt
) : IEvent;

public record TripParticipantRemoved(
    string TripId,
    string UserId,
    DateTime RemovedAt
) : IEvent;

public record TripStatusUpdated(
    string TripId,
    string OldStatus,
    string NewStatus,
    DateTime UpdatedAt
) : IEvent;

public record ItineraryDayAdded(
    string TripId,
    int Day,
    DateTime Date,
    DateTime AddedAt
) : IEvent;

// ============================================================================
// SUPPORTING TYPES
// ============================================================================

public record ItineraryActivity(
    string Time,
    string Title,
    LocationInfo Location,
    string Type, // "sightseeing", "transport", "accommodation", "dining"
    string? Notes
);
