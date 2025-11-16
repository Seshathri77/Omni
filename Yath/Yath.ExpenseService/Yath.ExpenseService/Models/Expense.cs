using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yath.ExpenseService.Models;

public class Expense
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("expenseId")]
    [BsonRequired]
    public string ExpenseId { get; set; } = string.Empty;

    [BsonElement("tripId")]
    [BsonRequired]
    public string TripId { get; set; } = string.Empty;

    [BsonElement("paidBy")]
    [BsonRequired]
    public string PaidBy { get; set; } = string.Empty;

    [BsonElement("amount")]
    [BsonRequired]
    public decimal Amount { get; set; }

    [BsonElement("currency")]
    public string Currency { get; set; } = "USD";

    [BsonElement("category")]
    [BsonRepresentation(BsonType.String)]
    public ExpenseCategory Category { get; set; }

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("splits")]
    public List<ExpenseSplit> Splits { get; set; } = new();

    [BsonElement("receiptUrl")]
    public string? ReceiptUrl { get; set; }

    [BsonElement("date")]
    public DateTime Date { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ExpenseSplit
{
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("amount")]
    public decimal Amount { get; set; }

    [BsonElement("paid")]
    public bool Paid { get; set; }
}

public enum ExpenseCategory
{
    Accommodation,
    Transportation,
    Food,
    Activities,
    Shopping,
    Other
}
