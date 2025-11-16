using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OmniFlow.Adapters.MongoDb;

/// <summary>
/// Represents an idempotency record in MongoDB.
/// </summary>
public class IdempotencyRecord
{
    /// <summary>
    /// MongoDB document ID.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// Unique message identifier.
    /// </summary>
    [BsonElement("messageId")]
    [BsonRequired]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the consumer/service that processed the message.
    /// </summary>
    [BsonElement("consumerName")]
    [BsonRequired]
    public string ConsumerName { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the message was first processed.
    /// </summary>
    [BsonElement("processedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ProcessedAt { get; set; }

    /// <summary>
    /// Timestamp when this record should expire (TTL index).
    /// </summary>
    [BsonElement("expiresAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; set; }
}
