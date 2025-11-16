using OmniFlow.Core;

namespace Yath.Shared.Messages;

// ============================================================================
// LOCATION COMMANDS
// ============================================================================

public record StartLocationSharing(
    string TripId,
    string UserId
) : ICommand;

public record StopLocationSharing(
    string TripId,
    string UserId
) : ICommand;

public record UpdateLocation(
    string SessionId,
    string UserId,
    double Latitude,
    double Longitude,
    double Accuracy,
    DateTime Timestamp
) : ICommand;

// ============================================================================
// LOCATION EVENTS
// ============================================================================

public record LocationSharingStarted(
    string SessionId,
    string TripId,
    string UserId,
    DateTime StartedAt
) : IEvent;

public record LocationSharingStopped(
    string SessionId,
    string TripId,
    string UserId,
    DateTime StoppedAt
) : IEvent;

public record LocationUpdated(
    string SessionId,
    string UserId,
    double Latitude,
    double Longitude,
    double Accuracy,
    DateTime Timestamp
) : IEvent;
