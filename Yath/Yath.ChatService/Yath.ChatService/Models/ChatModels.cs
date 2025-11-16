using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yath.ChatService.Models;

public class ChatRoom
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("roomId")]
    [BsonRequired]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("tripId")]
    [BsonRequired]
    public string TripId { get; set; } = string.Empty;

    [BsonElement("participantIds")]
    public List<string> ParticipantIds { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Message
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("messageId")]
    [BsonRequired]
    public string MessageId { get; set; } = string.Empty;

    [BsonElement("roomId")]
    [BsonRequired]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("userId")]
    [BsonRequired]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("text")]
    public string? Text { get; set; }

    [BsonElement("mediaUrl")]
    public string? MediaUrl { get; set; }

    [BsonElement("location")]
    public MessageLocation? Location { get; set; }

    [BsonElement("replyToMessageId")]
    public string? ReplyToMessageId { get; set; }

    [BsonElement("readBy")]
    public List<string> ReadBy { get; set; } = new();

    [BsonElement("reactions")]
    public List<MessageReaction> Reactions { get; set; } = new();

    [BsonElement("isDeleted")]
    public bool IsDeleted { get; set; } = false;

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class MessageLocation
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("latitude")]
    public double Latitude { get; set; }

    [BsonElement("longitude")]
    public double Longitude { get; set; }
}

public class MessageReaction
{
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("emoji")]
    public string Emoji { get; set; } = string.Empty;

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class UserPresence
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("userId")]
    [BsonRequired]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("roomId")]
    [BsonRequired]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public PresenceStatus Status { get; set; } = PresenceStatus.Offline;

    [BsonElement("lastSeen")]
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    [BsonElement("connectionId")]
    public string? ConnectionId { get; set; }
}

public enum PresenceStatus
{
    Online,
    Away,
    Offline
}
