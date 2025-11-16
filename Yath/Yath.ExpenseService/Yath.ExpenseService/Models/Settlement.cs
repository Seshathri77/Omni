using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yath.ExpenseService.Models;

public class Settlement
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("settlementId")]
    [BsonRequired]
    public string SettlementId { get; set; } = string.Empty;

    [BsonElement("tripId")]
    [BsonRequired]
    public string TripId { get; set; } = string.Empty;

    [BsonElement("from")]
    [BsonRequired]
    public string From { get; set; } = string.Empty;

    [BsonElement("to")]
    [BsonRequired]
    public string To { get; set; } = string.Empty;

    [BsonElement("amount")]
    public decimal Amount { get; set; }

    [BsonElement("currency")]
    public string Currency { get; set; } = "USD";

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public SettlementStatus Status { get; set; } = SettlementStatus.Pending;

    [BsonElement("settledAt")]
    public DateTime? SettledAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum SettlementStatus
{
    Pending,
    Completed,
    Cancelled
}
