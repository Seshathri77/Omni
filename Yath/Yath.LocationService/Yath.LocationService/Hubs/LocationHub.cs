using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OmniFlow.Core;
using OmniFlow.Messaging;
using Yath.LocationService.Models;
using Yath.LocationService.Repositories;
using Yath.Shared.Messages;

namespace Yath.LocationService.Hubs;

[Authorize]
public class LocationHub : Hub
{
    private readonly ITrackingSessionRepository _sessionRepository;
    private readonly ILocationUpdateRepository _locationRepository;
    private readonly ILocationHistoryRepository _historyRepository;
    private readonly IMessageBus _messageBus;
    private readonly ICorrelationAccessor _correlationAccessor;
    private readonly ILogger<LocationHub> _logger;

    public LocationHub(
        ITrackingSessionRepository sessionRepository,
        ILocationUpdateRepository locationRepository,
        ILocationHistoryRepository historyRepository,
        IMessageBus messageBus,
        ICorrelationAccessor correlationAccessor,
        ILogger<LocationHub> logger)
    {
        _sessionRepository = sessionRepository;
        _locationRepository = locationRepository;
        _historyRepository = historyRepository;
        _messageBus = messageBus;
        _correlationAccessor = correlationAccessor;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Connection attempt without valid user ID");
            Context.Abort();
            return;
        }

        _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            // End any active tracking sessions
            var activeSession = await _sessionRepository.GetActiveByUserIdAsync(userId);
            if (activeSession != null)
            {
                await EndTracking(activeSession.SessionId);
            }

            _logger.LogInformation("User {UserId} disconnected", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Start location tracking for a trip
    /// </summary>
    public async Task<string> StartTracking(string? tripId, string sharingMode)
    {
        var userId = Context.User?.FindFirst("sub")?.Value!;

        // Check if user already has an active session
        var existingSession = await _sessionRepository.GetActiveByUserIdAsync(userId);
        if (existingSession != null)
        {
            _logger.LogWarning("User {UserId} already has an active tracking session", userId);
            return existingSession.SessionId;
        }

        // Create new tracking session
        var session = new TrackingSession
        {
            UserId = userId,
            TripId = tripId,
            SharingMode = Enum.Parse<SharingMode>(sharingMode, true),
            ConnectionId = Context.ConnectionId,
            StartedAt = DateTime.UtcNow,
            LastUpdateAt = DateTime.UtcNow
        };

        await _sessionRepository.CreateAsync(session);

        // Join trip group if tracking for a trip
        if (!string.IsNullOrEmpty(tripId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"trip-{tripId}");
            
            // Notify trip participants
            await Clients.Group($"trip-{tripId}").SendAsync("TrackingStarted", new
            {
                userId,
                sessionId = session.SessionId,
                tripId,
                startedAt = session.StartedAt
            });
        }

        // Create location history entry
        var history = new LocationHistory
        {
            UserId = userId,
            TripId = tripId ?? string.Empty,
            SessionId = session.SessionId,
            StartTime = DateTime.UtcNow
        };
        await _historyRepository.CreateAsync(history);

        _logger.LogInformation("User {UserId} started tracking session {SessionId}", userId, session.SessionId);
        return session.SessionId;
    }

    /// <summary>
    /// Update user's location
    /// </summary>
    public async Task UpdateLocation(LocationUpdateDto locationDto)
    {
        var userId = Context.User?.FindFirst("sub")?.Value!;

        var activeSession = await _sessionRepository.GetActiveByUserIdAsync(userId);
        if (activeSession == null)
        {
            _logger.LogWarning("User {UserId} attempted to update location without active session", userId);
            throw new HubException("No active tracking session. Start tracking first.");
        }

        // Create location update
        var location = new LocationUpdate
        {
            UserId = userId,
            TripId = activeSession.TripId,
            Latitude = locationDto.Latitude,
            Longitude = locationDto.Longitude,
            Accuracy = locationDto.Accuracy,
            Altitude = locationDto.Altitude,
            Speed = locationDto.Speed,
            Heading = locationDto.Heading,
            BatteryLevel = locationDto.BatteryLevel,
            IsMoving = locationDto.Speed.HasValue && locationDto.Speed.Value > 0.5, // > 0.5 m/s
            Timestamp = DateTime.UtcNow
        };

        await _locationRepository.CreateAsync(location);

        // Calculate distance from last update
        double distance = 0;
        var recentLocations = await _locationRepository.GetRecentByUserIdAsync(userId, TimeSpan.FromMinutes(5));
        if (recentLocations.Count > 1)
        {
            var lastLocation = recentLocations[1]; // [0] is current, [1] is previous
            distance = CalculateDistance(lastLocation.Latitude, lastLocation.Longitude, location.Latitude, location.Longitude);
        }

        // Update session stats
        activeSession.LastUpdateAt = DateTime.UtcNow;
        activeSession.TotalDistance += distance;
        activeSession.LocationCount++;
        await _sessionRepository.UpdateAsync(activeSession);

        // Update location history
        var history = await _historyRepository.GetBySessionIdAsync(activeSession.SessionId);
        if (history != null)
        {
            history.Points.Add(new LocationPoint
            {
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Accuracy = location.Accuracy,
                Altitude = location.Altitude,
                Speed = location.Speed,
                Heading = location.Heading,
                Timestamp = location.Timestamp
            });
            history.EndTime = DateTime.UtcNow;
            history.TotalDistance = activeSession.TotalDistance;
            
            if (location.Speed.HasValue)
            {
                history.MaxSpeed = Math.Max(history.MaxSpeed, location.Speed.Value);
                var speeds = history.Points.Where(p => p.Speed.HasValue).Select(p => p.Speed!.Value).ToList();
                history.AverageSpeed = speeds.Any() ? speeds.Average() : 0;
            }

            await _historyRepository.UpdateAsync(history);
        }

        // Broadcast location based on sharing mode
        await BroadcastLocation(activeSession, location);

        // Publish LocationUpdated event
        var evt = new LocationUpdated(
            activeSession.SessionId,
            userId,
            location.Latitude,
            location.Longitude,
            location.Accuracy,
            location.Timestamp
        );

        await _messageBus.PublishAsync(MessageEnvelope<LocationUpdated>.Create(evt, _correlationAccessor));

        _logger.LogDebug("User {UserId} location updated: {Lat}, {Lng}", userId, location.Latitude, location.Longitude);
    }

    /// <summary>
    /// End location tracking
    /// </summary>
    public async Task EndTracking(string sessionId)
    {
        var userId = Context.User?.FindFirst("sub")?.Value!;

        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null || session.UserId != userId)
        {
            throw new HubException("Session not found or unauthorized");
        }

        if (!session.IsActive)
        {
            _logger.LogWarning("Attempted to end already inactive session {SessionId}", sessionId);
            return;
        }

        // Mark session as ended
        await _sessionRepository.EndSessionAsync(sessionId);

        // Leave trip group
        if (!string.IsNullOrEmpty(session.TripId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"trip-{session.TripId}");
            
            // Notify trip participants
            await Clients.Group($"trip-{session.TripId}").SendAsync("TrackingStopped", new
            {
                userId,
                sessionId,
                tripId = session.TripId,
                endedAt = DateTime.UtcNow,
                totalDistance = session.TotalDistance,
                duration = DateTime.UtcNow - session.StartedAt
            });
        }

        _logger.LogInformation("User {UserId} ended tracking session {SessionId}", userId, sessionId);
    }

    /// <summary>
    /// Get live locations for all users in a trip
    /// </summary>
    public async Task<List<object>> GetTripLiveLocations(string tripId)
    {
        var activeSessions = await _sessionRepository.GetActiveByTripIdAsync(tripId);
        var liveLocations = new List<object>();

        foreach (var session in activeSessions)
        {
            var recentLocations = await _locationRepository.GetRecentByUserIdAsync(session.UserId, TimeSpan.FromMinutes(5));
            if (recentLocations.Any())
            {
                var latest = recentLocations.First();
                liveLocations.Add(new
                {
                    userId = session.UserId,
                    latitude = latest.Latitude,
                    longitude = latest.Longitude,
                    accuracy = latest.Accuracy,
                    speed = latest.Speed,
                    heading = latest.Heading,
                    timestamp = latest.Timestamp,
                    isMoving = latest.IsMoving
                });
            }
        }

        return liveLocations;
    }

    /// <summary>
    /// Join a trip's location tracking group
    /// </summary>
    public async Task JoinTripTracking(string tripId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"trip-{tripId}");
        _logger.LogInformation("Connection {ConnectionId} joined trip tracking group {TripId}", Context.ConnectionId, tripId);
    }

    /// <summary>
    /// Leave a trip's location tracking group
    /// </summary>
    public async Task LeaveTripTracking(string tripId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"trip-{tripId}");
        _logger.LogInformation("Connection {ConnectionId} left trip tracking group {TripId}", Context.ConnectionId, tripId);
    }

    private async Task BroadcastLocation(TrackingSession session, LocationUpdate location)
    {
        var locationData = new
        {
            userId = session.UserId,
            latitude = location.Latitude,
            longitude = location.Longitude,
            accuracy = location.Accuracy,
            altitude = location.Altitude,
            speed = location.Speed,
            heading = location.Heading,
            timestamp = location.Timestamp,
            isMoving = location.IsMoving,
            batteryLevel = location.BatteryLevel
        };

        switch (session.SharingMode)
        {
            case SharingMode.TripParticipants:
                if (!string.IsNullOrEmpty(session.TripId))
                {
                    await Clients.Group($"trip-{session.TripId}").SendAsync("LocationUpdate", locationData);
                }
                break;

            case SharingMode.Public:
                await Clients.All.SendAsync("LocationUpdate", locationData);
                break;

            case SharingMode.Private:
                await Clients.Caller.SendAsync("LocationUpdate", locationData);
                break;
        }
    }

    /// <summary>
    /// Calculate distance between two coordinates using Haversine formula
    /// </summary>
    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371e3; // Earth's radius in meters
        var φ1 = lat1 * Math.PI / 180;
        var φ2 = lat2 * Math.PI / 180;
        var Δφ = (lat2 - lat1) * Math.PI / 180;
        var Δλ = (lon2 - lon1) * Math.PI / 180;

        var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                Math.Cos(φ1) * Math.Cos(φ2) *
                Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }
}

/// <summary>
/// DTO for location updates from clients
/// </summary>
public record LocationUpdateDto(
    double Latitude,
    double Longitude,
    double Accuracy,
    double? Altitude,
    double? Speed,
    double? Heading,
    int? BatteryLevel
);
