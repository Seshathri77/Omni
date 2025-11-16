using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yath.ExpenseService.Models;

public class ExpenseGroup
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("groupId")]
    [BsonRequired]
    public string GroupId { get; set; } = string.Empty;

    [BsonElement("tripId")]
    [BsonRequired]
    public string TripId { get; set; } = string.Empty;

    [BsonElement("members")]
    public List<string> Members { get; set; } = new();

    [BsonElement("totalExpenses")]
    public decimal TotalExpenses { get; set; }

    [BsonElement("currency")]
    public string Currency { get; set; } = "USD";

    [BsonElement("balances")]
    public Dictionary<string, decimal> Balances { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
