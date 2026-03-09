using OmniFlow.Core;

namespace ECommerce.Contracts;

// Payment Commands
public record RequestPayment(
    string PaymentId,
    string OrderId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod
) : ICommand;

public record RefundPayment(
    string PaymentId,
    string OrderId,
    decimal Amount,
    string Reason
) : ICommand;

// Payment Events
public record PaymentRequested(
    string PaymentId,
    string OrderId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod,
    DateTimeOffset RequestedAt
) : IEvent;

public record PaymentSucceeded(
    string PaymentId,
    string OrderId,
    decimal Amount,
    string TransactionId,
    DateTimeOffset ProcessedAt
) : IEvent;

public record PaymentFailed(
    string PaymentId,
    string OrderId,
    decimal Amount,
    string Reason,
    DateTimeOffset FailedAt
) : IEvent;

public record PaymentRefunded(
    string PaymentId,
    string OrderId,
    decimal Amount,
    string Reason,
    DateTimeOffset RefundedAt
) : IEvent;

// Inventory Commands
public record ReserveInventory(
    string OrderId,
    List<InventoryItem> Items
) : ICommand;

public record ReleaseInventory(
    string OrderId,
    List<InventoryItem> Items
) : ICommand;

// Inventory Events
public record InventoryReserved(
    string OrderId,
    List<InventoryItem> Items,
    DateTimeOffset ReservedAt
) : IEvent;

public record InventoryReservationFailed(
    string OrderId,
    List<InventoryItem> Items,
    string Reason,
    DateTimeOffset FailedAt
) : IEvent;

public record InventoryReleased(
    string OrderId,
    List<InventoryItem> Items,
    DateTimeOffset ReleasedAt
) : IEvent;

// Value Objects
public record PaymentMethod(
    string Type, // CreditCard, DebitCard, PayPal, etc.
    string Last4Digits
);

public record InventoryItem(
    string ProductId,
    int Quantity
);
