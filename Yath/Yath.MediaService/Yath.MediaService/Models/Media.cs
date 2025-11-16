using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yath.MediaService.Models;

public class Media
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("mediaId")]
    [BsonRequired]
    public string MediaId { get; set; } = string.Empty;

    [BsonElement("userId")]
    [BsonRequired]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("tripId")]
    public string? TripId { get; set; }

    [BsonElement("activityId")]
    public string? ActivityId { get; set; }

    [BsonElement("type")]
    [BsonRepresentation(BsonType.String)]
    public MediaType Type { get; set; }

    [BsonElement("url")]
    [BsonRequired]
    public string Url { get; set; } = string.Empty;

    [BsonElement("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }

    [BsonElement("blobName")]
    [BsonRequired]
    public string BlobName { get; set; } = string.Empty;

    [BsonElement("thumbnailBlobName")]
    public string? ThumbnailBlobName { get; set; }

    [BsonElement("fileName")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [BsonElement("sizeInBytes")]
    public long SizeInBytes { get; set; }

    [BsonElement("width")]
    public int Width { get; set; }

    [BsonElement("height")]
    public int Height { get; set; }

    [BsonElement("duration")]
    public int? Duration { get; set; } // For videos, in seconds

    [BsonElement("caption")]
    public string? Caption { get; set; }

    [BsonElement("tags")]
    public List<string> Tags { get; set; } = new();

    [BsonElement("location")]
    public MediaLocation? Location { get; set; }

    [BsonElement("uploadStatus")]
    [BsonRepresentation(BsonType.String)]
    public UploadStatus UploadStatus { get; set; } = UploadStatus.Uploading;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class MediaLocation
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("latitude")]
    public double Latitude { get; set; }

    [BsonElement("longitude")]
    public double Longitude { get; set; }

    [BsonElement("placeId")]
    public string? PlaceId { get; set; }
}

public enum MediaType
{
    Photo,
    Video
}

public enum UploadStatus
{
    Uploading,
    Processing,
    Completed,
    Failed
}
