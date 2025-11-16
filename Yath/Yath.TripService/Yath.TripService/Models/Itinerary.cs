using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yath.TripService.Models;

public class Itinerary
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("itineraryId")]
    [BsonRequired]
    public string ItineraryId { get; set; } = string.Empty;

    [BsonElement("tripId")]
    [BsonRequired]
    public string TripId { get; set; } = string.Empty;

    [BsonElement("day")]
    public int Day { get; set; }

    [BsonElement("date")]
    public DateTime Date { get; set; }

    [BsonElement("activities")]
    public List<ItineraryActivity> Activities { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ItineraryActivity
{
    [BsonElement("time")]
    public string Time { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("location")]
    public ActivityLocation Location { get; set; } = new();

    [BsonElement("type")]
    [BsonRepresentation(BsonType.String)]
    public ActivityType Type { get; set; }

    [BsonElement("notes")]
    public string? Notes { get; set; }

    [BsonElement("bookingInfo")]
    public string? BookingInfo { get; set; }
}

public class ActivityLocation
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("latitude")]
    public double Latitude { get; set; }

    [BsonElement("longitude")]
    public double Longitude { get; set; }
}

public enum ActivityType
{
    Sightseeing,
    Transport,
    Accommodation,
    Dining,
    Other
}
