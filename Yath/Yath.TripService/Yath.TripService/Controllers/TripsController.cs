using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniFlow.Core;
using OmniFlow.Messaging;
using Yath.Shared.DTOs;
using Yath.Shared.Messages;
using Yath.TripService.Models;
using Yath.TripService.Repositories;

namespace Yath.TripService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TripsController : ControllerBase
{
    private readonly ITripRepository _tripRepository;
    private readonly IItineraryRepository _itineraryRepository;
    private readonly IMessageBus _messageBus;
    private readonly ICorrelationAccessor _correlationAccessor;
    private readonly ILogger<TripsController> _logger;

    public TripsController(
        ITripRepository tripRepository,
        IItineraryRepository itineraryRepository,
        IMessageBus messageBus,
        ICorrelationAccessor correlationAccessor,
        ILogger<TripsController> logger)
    {
        _tripRepository = tripRepository;
        _itineraryRepository = itineraryRepository;
        _messageBus = messageBus;
        _correlationAccessor = correlationAccessor;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TripDto>>> CreateTrip([FromBody] CreateTripRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var trip = new Trip
            {
                TripId = Guid.NewGuid().ToString(),
                CreatorId = userId,
                Title = request.Title,
                Description = request.Description,
                Dates = new TripDates
                {
                    StartDate = request.StartDate,
                    EndDate = request.EndDate
                },
                Destinations = request.Destinations,
                Participants = new List<TripParticipant>
                {
                    new TripParticipant
                    {
                        UserId = userId,
                        Role = ParticipantRole.Owner,
                        JoinedAt = DateTime.UtcNow
                    }
                },
                Status = TripStatus.Planning,
                Visibility = request.Visibility == "public" ? TripVisibility.Public : TripVisibility.Private
            };

            await _tripRepository.CreateAsync(trip);

            // Publish TripCreated event
            await _messageBus.PublishAsync(new TripCreated(
                trip.TripId,
                trip.CreatorId,
                trip.Title,
                trip.Description,
                trip.Dates.StartDate,
                trip.Dates.EndDate,
                trip.Destinations,
                DateTime.UtcNow
            ));

            // Request chat room creation
            await _messageBus.PublishAsync(new CreateChatRoom(
                trip.TripId,
                new List<string> { userId }
            ));

            _logger.LogInformation("Trip {TripId} created by user {UserId}", trip.TripId, userId);

            var tripDto = MapToDto(trip);
            return Ok(new ApiResponse<TripDto>(true, tripDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating trip");
            return StatusCode(500, new ApiResponse<TripDto>(false, null, "Failed to create trip"));
        }
    }

    [HttpGet("{tripId}")]
    public async Task<ActionResult<ApiResponse<TripDto>>> GetTrip(string tripId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var trip = await _tripRepository.GetByIdAsync(tripId);
            if (trip == null)
                return NotFound(new ApiResponse<TripDto>(false, null, "Trip not found"));

            // Check if user has access
            if (trip.Visibility == TripVisibility.Private && !await _tripRepository.IsParticipantAsync(tripId, userId))
                return Forbid();

            var tripDto = MapToDto(trip);
            return Ok(new ApiResponse<TripDto>(true, tripDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trip");
            return StatusCode(500, new ApiResponse<TripDto>(false, null, "Failed to fetch trip"));
        }
    }

    [HttpPut("{tripId}")]
    public async Task<ActionResult<ApiResponse<TripDto>>> UpdateTrip(string tripId, [FromBody] UpdateTripRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var trip = await _tripRepository.GetByIdAsync(tripId);
            if (trip == null)
                return NotFound(new ApiResponse<TripDto>(false, null, "Trip not found"));

            // Check if user is owner or editor
            var participant = trip.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant == null || participant.Role == ParticipantRole.Viewer)
                return Forbid();

            // Update fields
            if (!string.IsNullOrEmpty(request.Title))
                trip.Title = request.Title;
            
            if (!string.IsNullOrEmpty(request.Description))
                trip.Description = request.Description;
            
            if (request.StartDate.HasValue)
                trip.Dates.StartDate = request.StartDate.Value;
            
            if (request.EndDate.HasValue)
                trip.Dates.EndDate = request.EndDate.Value;

            await _tripRepository.UpdateAsync(trip);

            // Publish event
            await _messageBus.PublishAsync(new TripUpdated(
                trip.TripId,
                DateTime.UtcNow
            ));

            _logger.LogInformation("Trip {TripId} updated by user {UserId}", tripId, userId);

            var tripDto = MapToDto(trip);
            return Ok(new ApiResponse<TripDto>(true, tripDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating trip");
            return StatusCode(500, new ApiResponse<TripDto>(false, null, "Failed to update trip"));
        }
    }

    [HttpPost("{tripId}/participants")]
    public async Task<ActionResult<ApiResponse<bool>>> AddParticipant(string tripId, [FromBody] AddParticipantRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var trip = await _tripRepository.GetByIdAsync(tripId);
            if (trip == null)
                return NotFound(new ApiResponse<bool>(false, false, "Trip not found"));

            // Check if user is owner or editor
            var currentParticipant = trip.Participants.FirstOrDefault(p => p.UserId == userId);
            if (currentParticipant == null || currentParticipant.Role == ParticipantRole.Viewer)
                return Forbid();

            // Check if already a participant
            if (trip.Participants.Any(p => p.UserId == request.UserId))
                return BadRequest(new ApiResponse<bool>(false, false, "User is already a participant"));

            // Add participant
            var role = Enum.Parse<ParticipantRole>(request.Role, true);
            trip.Participants.Add(new TripParticipant
            {
                UserId = request.UserId,
                Role = role,
                JoinedAt = DateTime.UtcNow
            });

            await _tripRepository.UpdateAsync(trip);

            // Publish event
            await _messageBus.PublishAsync(new TripParticipantAdded(
                trip.TripId,
                request.UserId,
                request.Role,
                DateTime.UtcNow
            ));

            // Send notification
            await _messageBus.PublishAsync(new SendNotification(
                request.UserId,
                "trip_invite",
                "Trip Invitation",
                $"You've been added to {trip.Title}",
                new Dictionary<string, string>
                {
                    { "tripId", trip.TripId },
                    { "addedBy", userId }
                }
            ));

            _logger.LogInformation("User {UserId} added to trip {TripId}", request.UserId, tripId);

            return Ok(new ApiResponse<bool>(true, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding participant");
            return StatusCode(500, new ApiResponse<bool>(false, false, "Failed to add participant"));
        }
    }

    [HttpDelete("{tripId}/participants/{participantId}")]
    public async Task<ActionResult<ApiResponse<bool>>> RemoveParticipant(string tripId, string participantId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var trip = await _tripRepository.GetByIdAsync(tripId);
            if (trip == null)
                return NotFound(new ApiResponse<bool>(false, false, "Trip not found"));

            // Only owner can remove participants
            if (trip.CreatorId != userId)
                return Forbid();

            // Cannot remove owner
            if (participantId == trip.CreatorId)
                return BadRequest(new ApiResponse<bool>(false, false, "Cannot remove trip owner"));

            var participant = trip.Participants.FirstOrDefault(p => p.UserId == participantId);
            if (participant == null)
                return NotFound(new ApiResponse<bool>(false, false, "Participant not found"));

            trip.Participants.Remove(participant);
            await _tripRepository.UpdateAsync(trip);

            // Publish event
            await _messageBus.PublishAsync(new TripParticipantRemoved(
                trip.TripId,
                participantId,
                DateTime.UtcNow
            ));

            _logger.LogInformation("User {ParticipantId} removed from trip {TripId}", participantId, tripId);

            return Ok(new ApiResponse<bool>(true, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing participant");
            return StatusCode(500, new ApiResponse<bool>(false, false, "Failed to remove participant"));
        }
    }

    [HttpPatch("{tripId}/status")]
    public async Task<ActionResult<ApiResponse<TripDto>>> UpdateStatus(string tripId, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var trip = await _tripRepository.GetByIdAsync(tripId);
            if (trip == null)
                return NotFound(new ApiResponse<TripDto>(false, null, "Trip not found"));

            // Only owner can update status
            if (trip.CreatorId != userId)
                return Forbid();

            var oldStatus = trip.Status.ToString().ToLower();
            trip.Status = Enum.Parse<TripStatus>(request.Status, true);
            
            await _tripRepository.UpdateAsync(trip);

            // Publish event
            await _messageBus.PublishAsync(new TripStatusUpdated(
                trip.TripId,
                oldStatus,
                request.Status.ToLower(),
                DateTime.UtcNow
            ));

            _logger.LogInformation("Trip {TripId} status updated to {Status}", tripId, request.Status);

            var tripDto = MapToDto(trip);
            return Ok(new ApiResponse<TripDto>(true, tripDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating trip status");
            return StatusCode(500, new ApiResponse<TripDto>(false, null, "Failed to update status"));
        }
    }

    [HttpGet("my-trips")]
    public async Task<ActionResult<ApiResponse<List<TripDto>>>> GetMyTrips([FromQuery] int skip = 0, [FromQuery] int limit = 20)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var trips = await _tripRepository.GetByParticipantAsync(userId, skip, limit);
            var tripDtos = trips.Select(MapToDto).ToList();

            return Ok(new ApiResponse<List<TripDto>>(true, tripDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user trips");
            return StatusCode(500, new ApiResponse<List<TripDto>>(false, null, "Failed to fetch trips"));
        }
    }

    [HttpPost("{tripId}/itinerary")]
    public async Task<ActionResult<ApiResponse<ItineraryDayDto>>> AddItinerary(string tripId, [FromBody] AddItineraryRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var trip = await _tripRepository.GetByIdAsync(tripId);
            if (trip == null)
                return NotFound(new ApiResponse<ItineraryDayDto>(false, null, "Trip not found"));

            // Check if user is owner or editor
            var participant = trip.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant == null || participant.Role == ParticipantRole.Viewer)
                return Forbid();

            var itinerary = new Itinerary
            {
                ItineraryId = Guid.NewGuid().ToString(),
                TripId = tripId,
                Day = request.Day,
                Date = request.Date,
                Activities = request.Activities.Select(a => new Models.ItineraryActivity
                {
                    Time = a.Time,
                    Title = a.Title,
                    Location = new ActivityLocation
                    {
                        Name = a.Location.Name,
                        Latitude = a.Location.Latitude,
                        Longitude = a.Location.Longitude
                    },
                    Type = Enum.Parse<ActivityType>(a.Type, true),
                    Notes = a.Notes,
                    BookingInfo = a.BookingInfo
                }).ToList()
            };

            await _itineraryRepository.CreateAsync(itinerary);

            // Publish event
            await _messageBus.PublishAsync(new ItineraryDayAdded(
                tripId,
                request.Day,
                request.Date,
                DateTime.UtcNow
            ));

            _logger.LogInformation("Itinerary day {Day} added to trip {TripId}", request.Day, tripId);

            var itineraryDto = MapItineraryToDto(itinerary);
            return Ok(new ApiResponse<ItineraryDayDto>(true, itineraryDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding itinerary");
            return StatusCode(500, new ApiResponse<ItineraryDayDto>(false, null, "Failed to add itinerary"));
        }
    }

    [HttpGet("{tripId}/itinerary")]
    public async Task<ActionResult<ApiResponse<List<ItineraryDayDto>>>> GetItinerary(string tripId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Check if user has access to trip
            if (!await _tripRepository.IsParticipantAsync(tripId, userId))
            {
                var trip = await _tripRepository.GetByIdAsync(tripId);
                if (trip == null || trip.Visibility == TripVisibility.Private)
                    return Forbid();
            }

            var itineraries = await _itineraryRepository.GetByTripIdAsync(tripId);
            var itineraryDtos = itineraries.Select(MapItineraryToDto).ToList();

            return Ok(new ApiResponse<List<ItineraryDayDto>>(true, itineraryDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching itinerary");
            return StatusCode(500, new ApiResponse<List<ItineraryDayDto>>(false, null, "Failed to fetch itinerary"));
        }
    }

    private TripDto MapToDto(Trip trip)
    {
        return new TripDto(
            trip.TripId,
            trip.CreatorId,
            trip.Title,
            trip.Description,
            trip.Dates.StartDate,
            trip.Dates.EndDate,
            trip.Destinations,
            trip.Participants.Select(p => new TripParticipantDto(
                p.UserId,
                string.Empty, // Username will be enriched by client
                string.Empty, // DisplayName will be enriched by client
                null, // AvatarUrl will be enriched by client
                p.Role.ToString().ToLower(),
                p.JoinedAt
            )).ToList(),
            trip.Status.ToString().ToLower(),
            trip.Visibility.ToString().ToLower(),
            trip.CoverImageUrl,
            trip.CreatedAt
        );
    }

    private ItineraryDayDto MapItineraryToDto(Itinerary itinerary)
    {
        return new ItineraryDayDto(
            itinerary.Day,
            itinerary.Date,
            itinerary.Activities.Select(a => new ItineraryActivityDto(
                a.Time,
                a.Title,
                new LocationInfoDto(a.Location.Name, a.Location.Latitude, a.Location.Longitude, null),
                a.Type.ToString().ToLower(),
                a.Notes,
                a.BookingInfo
            )).ToList()
        );
    }
}

public record AddParticipantRequest(string UserId, string Role);
public record UpdateStatusRequest(string Status);
