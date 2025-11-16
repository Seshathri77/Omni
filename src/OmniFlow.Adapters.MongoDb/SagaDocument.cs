using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OmniFlow.Adapters.MongoDb;

/// <summary>
/// MongoDB document wrapper for saga state with metadata.
/// </summary>
public class SagaDocument<TState> where TState : class
{
    /// <summary>
    /// MongoDB document ID.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// Saga unique identifier.
    /// </summary>
    [BsonElement("sagaId")]
    [BsonRequired]
    public string SagaId { get; set; } = string.Empty;

    /// <summary>
    /// Type name of the saga for querying.
    /// </summary>
    [BsonElement("sagaType")]
    public string SagaType { get; set; } = string.Empty;

    /// <summary>
    /// Version for optimistic concurrency control.
    /// </summary>
    [BsonElement("version")]
    public int Version { get; set; }

    /// <summary>
    /// Timestamp when saga was created.
    /// </summary>
    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when saga was last updated.
    /// </summary>
    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// The actual saga state.
    /// </summary>
    [BsonElement("state")]
    [BsonRequired]
    public TState State { get; set; } = default!;
}
