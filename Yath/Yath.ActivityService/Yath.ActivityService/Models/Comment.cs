using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yath.ActivityService.Models;

public class Comment
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("commentId")]
    [BsonRequired]
    public string CommentId { get; set; } = string.Empty;

    [BsonElement("postId")]
    [BsonRequired]
    public string PostId { get; set; } = string.Empty;

    [BsonElement("userId")]
    [BsonRequired]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("content")]
    [BsonRequired]
    public string Content { get; set; } = string.Empty;

    [BsonElement("parentCommentId")]
    public string? ParentCommentId { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
