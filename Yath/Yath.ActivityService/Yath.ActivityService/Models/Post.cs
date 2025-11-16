using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yath.ActivityService.Models;

public class Post
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

    [BsonElement("content")]
    [BsonRequired]
    public string Content { get; set; } = string.Empty;

    [BsonElement("mediaUrls")]
    public List<string> MediaUrls { get; set; } = new();

    [BsonElement("tripId")]
    public string? TripId { get; set; }

    [BsonElement("location")]
    public PostLocation? Location { get; set; }

    [BsonElement("tags")]
    public List<string> Tags { get; set; } = new();

    [BsonElement("likesCount")]
    public int LikesCount { get; set; }

    [BsonElement("commentsCount")]
    public int CommentsCount { get; set; }

    [BsonElement("sharesCount")]
    public int SharesCount { get; set; }

    [BsonElement("visibility")]
    [BsonRepresentation(BsonType.String)]
    public PostVisibility Visibility { get; set; } = PostVisibility.Public;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PostLocation
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("latitude")]
    public double Latitude { get; set; }

    [BsonElement("longitude")]
    public double Longitude { get; set; }
}

public enum PostVisibility
{
    Public,
    Followers,
    Private
}
