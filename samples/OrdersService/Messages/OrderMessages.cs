using OmniFlow.Core;

namespace OrdersService.Messages;

// Commands
public record CreateOrder(string OrderId, decimal Amount, string CustomerId) : ICommand;
public record RequestPayment(string OrderId, decimal Amount) : ICommand;
public record CompleteOrder(string OrderId) : ICommand;
public record CancelOrder(string OrderId, string Reason) : ICommand;

// Events
public record OrderCreated(string OrderId, decimal Amount, string CustomerId) : IEvent;
public record PaymentRequested(string OrderId, decimal Amount) : ICommand;
public record PaymentSucceeded(string OrderId, string PaymentId) : IEvent;
public record PaymentFailed(string OrderId, string Reason) : IEvent;
public record OrderCompleted(string OrderId) : IEvent;
public record OrderCancelled(string OrderId, string Reason) : IEvent;
