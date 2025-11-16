using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yath.TripService.Models;

public class Trip
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("tripId")]
    [BsonRequired]
    public string TripId { get; set; } = string.Empty;

    [BsonElement("creatorId")]
    [BsonRequired]
    public string CreatorId { get; set; } = string.Empty;

    [BsonElement("title")]
    [BsonRequired]
    public string Title { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("dates")]
    public TripDates Dates { get; set; } = new();

    [BsonElement("destinations")]
    public List<string> Destinations { get; set; } = new();

    [BsonElement("participants")]
    public List<TripParticipant> Participants { get; set; } = new();

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public TripStatus Status { get; set; } = TripStatus.Planning;

    [BsonElement("visibility")]
    [BsonRepresentation(BsonType.String)]
    public TripVisibility Visibility { get; set; } = TripVisibility.Private;

    [BsonElement("coverImageUrl")]
    public string? CoverImageUrl { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class TripDates
{
    [BsonElement("startDate")]
    public DateTime StartDate { get; set; }

    [BsonElement("endDate")]
    public DateTime EndDate { get; set; }
}

public class TripParticipant
{
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("role")]
    [BsonRepresentation(BsonType.String)]
    public ParticipantRole Role { get; set; } = ParticipantRole.Viewer;

    [BsonElement("joinedAt")]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public enum TripStatus
{
    Planning,
    Ongoing,
    Completed,
    Cancelled
}

public enum TripVisibility
{
    Public,
    Private
}

public enum ParticipantRole
{
    Owner,
    Editor,
    Viewer
}
