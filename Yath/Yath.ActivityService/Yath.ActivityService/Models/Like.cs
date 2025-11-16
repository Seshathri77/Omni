using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yath.ActivityService.Models;

public class Like
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("postId")]
    [BsonRequired]
    public string PostId { get; set; } = string.Empty;

    [BsonElement("userId")]
    [BsonRequired]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("likedAt")]
    public DateTime LikedAt { get; set; } = DateTime.UtcNow;
}
