using OmniFlow.Core;

namespace ECommerce.Contracts;

// Commands
public record CreateOrder(
    string OrderId,
    string CustomerId,
    List<OrderItem> Items,
    decimal TotalAmount,
    ShippingAddress ShippingAddress
) : ICommand;

public record CancelOrder(string OrderId, string Reason) : ICommand;

public record ShipOrder(string OrderId, string TrackingNumber) : ICommand;

// Events
public record OrderCreated(
    string OrderId,
    string CustomerId,
    List<OrderItem> Items,
    decimal TotalAmount,
    ShippingAddress ShippingAddress,
    DateTimeOffset CreatedAt
) : IEvent;

public record OrderCancelled(
    string OrderId,
    string Reason,
    DateTimeOffset CancelledAt
) : IEvent;

public record OrderShipped(
    string OrderId,
    string TrackingNumber,
    DateTimeOffset ShippedAt
) : IEvent;

public record OrderCompleted(
    string OrderId,
    DateTimeOffset CompletedAt
) : IEvent;

public record OrderFailed(
    string OrderId,
    string Reason,
    DateTimeOffset FailedAt
) : IEvent;

// Value Objects
public record OrderItem(
    string ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
);

public record ShippingAddress(
    string Street,
    string City,
    string State,
    string ZipCode,
    string Country
);
