using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yath.LocationService.Models;
using Yath.LocationService.Repositories;
using Yath.Shared.DTOs;

namespace Yath.LocationService.Controllers;

[ApiController]
[Route("api/location")]
[Authorize]
public class LocationController : ControllerBase
{
    private readonly ITrackingSessionRepository _sessionRepository;
    private readonly ILocationUpdateRepository _locationRepository;
    private readonly ILocationHistoryRepository _historyRepository;
    private readonly ILogger<LocationController> _logger;

    public LocationController(
        ITrackingSessionRepository sessionRepository,
        ILocationUpdateRepository locationRepository,
        ILocationHistoryRepository historyRepository,
        ILogger<LocationController> logger)
    {
        _sessionRepository = sessionRepository;
        _locationRepository = locationRepository;
        _historyRepository = historyRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get active tracking session for current user
    /// </summary>
    [HttpGet("session/active")]
    public async Task<ActionResult<ApiResponse<TrackingSessionDto>>> GetActiveSession()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<TrackingSessionDto>(false, default, "Invalid user"));

        var session = await _sessionRepository.GetActiveByUserIdAsync(userId);
        if (session == null)
            return NotFound(new ApiResponse<TrackingSessionDto>(false, default, "No active tracking session"));

        var dto = new TrackingSessionDto(
            session.SessionId,
            session.UserId,
            session.TripId,
            session.StartedAt,
            session.EndedAt,
            session.IsActive,
            session.SharingMode.ToString(),
            session.TotalDistance,
            session.LocationCount
        );

        return Ok(new ApiResponse<TrackingSessionDto>(true, dto));
    }

    /// <summary>
    /// Get tracking session history for current user
    /// </summary>
    [HttpGet("sessions")]
    public async Task<ActionResult<ApiResponse<List<TrackingSessionDto>>>> GetUserSessions()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<List<TrackingSessionDto>>(false, default, "Invalid user"));

        var sessions = await _sessionRepository.GetByUserIdAsync(userId);
        var dtos = sessions.Select(s => new TrackingSessionDto(
            s.SessionId,
            s.UserId,
            s.TripId,
            s.StartedAt,
            s.EndedAt,
            s.IsActive,
            s.SharingMode.ToString(),
            s.TotalDistance,
            s.LocationCount
        )).ToList();

        return Ok(new ApiResponse<List<TrackingSessionDto>>(true, dtos));
    }

    /// <summary>
    /// Get location history for a trip
    /// </summary>
    [HttpGet("trip/{tripId}/history")]
    public async Task<ActionResult<ApiResponse<List<LocationHistoryDto>>>> GetTripLocationHistory(string tripId)
    {
        var histories = await _historyRepository.GetByTripIdAsync(tripId);
        var dtos = histories.Select(h => new LocationHistoryDto(
            h.HistoryId,
            h.UserId,
            h.TripId,
            h.SessionId,
            h.Points.Select(p => new LocationPointDto(
                p.Latitude,
                p.Longitude,
                p.Accuracy,
                p.Altitude,
                p.Speed,
                p.Heading,
                p.Timestamp
            )).ToList(),
            h.StartTime,
            h.EndTime,
            h.TotalDistance,
            h.AverageSpeed,
            h.MaxSpeed
        )).ToList();

        return Ok(new ApiResponse<List<LocationHistoryDto>>(true, dtos));
    }

    /// <summary>
    /// Get location history for a specific session
    /// </summary>
    [HttpGet("session/{sessionId}/history")]
    public async Task<ActionResult<ApiResponse<LocationHistoryDto>>> GetSessionHistory(string sessionId)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<LocationHistoryDto>(false, default, "Invalid user"));

        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null || session.UserId != userId)
            return NotFound(new ApiResponse<LocationHistoryDto>(false, default, "Session not found or unauthorized"));

        var history = await _historyRepository.GetBySessionIdAsync(sessionId);
        if (history == null)
            return NotFound(new ApiResponse<LocationHistoryDto>(false, default, "History not found"));

        var dto = new LocationHistoryDto(
            history.HistoryId,
            history.UserId,
            history.TripId,
            history.SessionId,
            history.Points.Select(p => new LocationPointDto(
                p.Latitude,
                p.Longitude,
                p.Accuracy,
                p.Altitude,
                p.Speed,
                p.Heading,
                p.Timestamp
            )).ToList(),
            history.StartTime,
            history.EndTime,
            history.TotalDistance,
            history.AverageSpeed,
            history.MaxSpeed
        );

        return Ok(new ApiResponse<LocationHistoryDto>(true, dto));
    }

    /// <summary>
    /// Get recent location updates for current user
    /// </summary>
    [HttpGet("recent")]
    public async Task<ActionResult<ApiResponse<List<LocationUpdateDto>>>> GetRecentLocations([FromQuery] int minutes = 60)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<List<LocationUpdateDto>>(false, default, "Invalid user"));

        var locations = await _locationRepository.GetRecentByUserIdAsync(userId, TimeSpan.FromMinutes(minutes));
        var dtos = locations.Select(l => new LocationUpdateDto(
            l.LocationId,
            l.UserId,
            l.TripId,
            l.Latitude,
            l.Longitude,
            l.Accuracy,
            l.Altitude,
            l.Speed,
            l.Heading,
            l.Timestamp,
            l.BatteryLevel,
            l.IsMoving
        )).ToList();

        return Ok(new ApiResponse<List<LocationUpdateDto>>(true, dtos));
    }

    /// <summary>
    /// Get location updates for a trip
    /// </summary>
    [HttpGet("trip/{tripId}/locations")]
    public async Task<ActionResult<ApiResponse<List<LocationUpdateDto>>>> GetTripLocations(string tripId, [FromQuery] int limit = 100)
    {
        var locations = await _locationRepository.GetByTripIdAsync(tripId, limit);
        var dtos = locations.Select(l => new LocationUpdateDto(
            l.LocationId,
            l.UserId,
            l.TripId,
            l.Latitude,
            l.Longitude,
            l.Accuracy,
            l.Altitude,
            l.Speed,
            l.Heading,
            l.Timestamp,
            l.BatteryLevel,
            l.IsMoving
        )).ToList();

        return Ok(new ApiResponse<List<LocationUpdateDto>>(true, dtos));
    }

    /// <summary>
    /// Delete location data for current user
    /// </summary>
    [HttpDelete("user/data")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteUserLocationData()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<string>(false, default, "Invalid user"));

        await _locationRepository.DeleteByUserIdAsync(userId);
        
        var histories = await _historyRepository.GetByUserIdAsync(userId);
        foreach (var history in histories)
        {
            await _historyRepository.DeleteAsync(history.HistoryId);
        }

        _logger.LogInformation("Deleted all location data for user {UserId}", userId);
        return Ok(new ApiResponse<string>(true, "Location data deleted successfully"));
    }
}

// DTOs
public record TrackingSessionDto(
    string SessionId,
    string UserId,
    string? TripId,
    DateTime StartedAt,
    DateTime? EndedAt,
    bool IsActive,
    string SharingMode,
    double TotalDistance,
    int LocationCount
);

public record LocationHistoryDto(
    string HistoryId,
    string UserId,
    string TripId,
    string SessionId,
    List<LocationPointDto> Points,
    DateTime StartTime,
    DateTime EndTime,
    double TotalDistance,
    double AverageSpeed,
    double MaxSpeed
);

public record LocationPointDto(
    double Latitude,
    double Longitude,
    double Accuracy,
    double? Altitude,
    double? Speed,
    double? Heading,
    DateTime Timestamp
);

public record LocationUpdateDto(
    string LocationId,
    string UserId,
    string? TripId,
    double Latitude,
    double Longitude,
    double Accuracy,
    double? Altitude,
    double? Speed,
    double? Heading,
    DateTime Timestamp,
    int? BatteryLevel,
    bool IsMoving
);
