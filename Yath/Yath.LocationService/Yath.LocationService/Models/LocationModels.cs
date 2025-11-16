using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yath.LocationService.Models;

/// <summary>
/// Represents a single location update from a user
/// </summary>
public class LocationUpdate
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string LocationId { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("tripId")]
    public string? TripId { get; set; }

    [BsonElement("latitude")]
    public double Latitude { get; set; }

    [BsonElement("longitude")]
    public double Longitude { get; set; }

    [BsonElement("accuracy")]
    public double Accuracy { get; set; } // in meters

    [BsonElement("altitude")]
    public double? Altitude { get; set; } // in meters

    [BsonElement("speed")]
    public double? Speed { get; set; } // in m/s

    [BsonElement("heading")]
    public double? Heading { get; set; } // in degrees

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("batteryLevel")]
    public int? BatteryLevel { get; set; } // percentage

    [BsonElement("isMoving")]
    public bool IsMoving { get; set; }
}

/// <summary>
/// Represents a tracking session for a user
/// </summary>
public class TrackingSession
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("tripId")]
    public string? TripId { get; set; }

    [BsonElement("startedAt")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("endedAt")]
    public DateTime? EndedAt { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("sharingMode")]
    public SharingMode SharingMode { get; set; } = SharingMode.TripParticipants;

    [BsonElement("connectionId")]
    public string? ConnectionId { get; set; }

    [BsonElement("lastUpdateAt")]
    public DateTime LastUpdateAt { get; set; } = DateTime.UtcNow;

    [BsonElement("totalDistance")]
    public double TotalDistance { get; set; } // in meters

    [BsonElement("locationCount")]
    public int LocationCount { get; set; }
}

/// <summary>
/// Defines who can see the user's location
/// </summary>
public enum SharingMode
{
    Private = 0,           // Only the user
    TripParticipants = 1,  // All participants in the associated trip
    Followers = 2,         // All followers
    Public = 3             // Everyone
}

/// <summary>
/// Stores location history for trips
/// </summary>
public class LocationHistory
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string HistoryId { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("tripId")]
    public string TripId { get; set; } = string.Empty;

    [BsonElement("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [BsonElement("points")]
    public List<LocationPoint> Points { get; set; } = new();

    [BsonElement("startTime")]
    public DateTime StartTime { get; set; }

    [BsonElement("endTime")]
    public DateTime EndTime { get; set; }

    [BsonElement("totalDistance")]
    public double TotalDistance { get; set; } // in meters

    [BsonElement("averageSpeed")]
    public double AverageSpeed { get; set; } // in m/s

    [BsonElement("maxSpeed")]
    public double MaxSpeed { get; set; } // in m/s
}

/// <summary>
/// A point in the location history
/// </summary>
public class LocationPoint
{
    [BsonElement("latitude")]
    public double Latitude { get; set; }

    [BsonElement("longitude")]
    public double Longitude { get; set; }

    [BsonElement("accuracy")]
    public double Accuracy { get; set; }

    [BsonElement("altitude")]
    public double? Altitude { get; set; }

    [BsonElement("speed")]
    public double? Speed { get; set; }

    [BsonElement("heading")]
    public double? Heading { get; set; }

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Represents a geofence (area monitoring)
/// </summary>
public class Geofence
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string GeofenceId { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("tripId")]
    public string? TripId { get; set; }

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [BsonElement("centerLatitude")]
    public double CenterLatitude { get; set; }

    [BsonElement("centerLongitude")]
    public double CenterLongitude { get; set; }

    [BsonElement("radiusMeters")]
    public double RadiusMeters { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("notifyOnEnter")]
    public bool NotifyOnEnter { get; set; } = true;

    [BsonElement("notifyOnExit")]
    public bool NotifyOnExit { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
