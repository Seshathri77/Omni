using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yath.UserService.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("userId")]
    [BsonRequired]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("username")]
    [BsonRequired]
    public string Username { get; set; } = string.Empty;

    [BsonElement("email")]
    [BsonRequired]
    public string Email { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    [BsonRequired]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("profile")]
    public UserProfile Profile { get; set; } = new();

    [BsonElement("socialGraph")]
    public SocialGraph SocialGraph { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class UserProfile
{
    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [BsonElement("bio")]
    public string? Bio { get; set; }

    [BsonElement("avatarUrl")]
    public string? AvatarUrl { get; set; }

    [BsonElement("location")]
    public string? Location { get; set; }

    [BsonElement("travelStyles")]
    public List<string> TravelStyles { get; set; } = new();
}

public class SocialGraph
{
    [BsonElement("followersCount")]
    public int FollowersCount { get; set; }

    [BsonElement("followingCount")]
    public int FollowingCount { get; set; }
}
