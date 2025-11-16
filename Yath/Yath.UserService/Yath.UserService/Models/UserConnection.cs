using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yath.UserService.Models;

public class UserConnection
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("followerId")]
    [BsonRequired]
    public string FollowerId { get; set; } = string.Empty;

    [BsonElement("followingId")]
    [BsonRequired]
    public string FollowingId { get; set; } = string.Empty;

    [BsonElement("followedAt")]
    public DateTime FollowedAt { get; set; } = DateTime.UtcNow;
}
