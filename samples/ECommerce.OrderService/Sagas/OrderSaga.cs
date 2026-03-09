using ECommerce.Contracts;
using OmniFlow.Sagas;
using Microsoft.Extensions.Logging;

namespace ECommerce.OrderService.Sagas;

/// <summary>
/// Order Saga orchestrates the order fulfillment process:
/// 1. Reserve inventory
/// 2. Process payment
/// 3. Ship order
/// With compensating actions for failures
/// </summary>
public class OrderSaga : Saga<OrderSagaState>
{
    private readonly ILogger<OrderSaga> _logger;

    public OrderSaga(ILogger<OrderSaga> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Start saga with pre-initialized state
    /// </summary>
    public async Task StartWithStateAsync(OrderSagaState state)
    {
        await StartAsync(state);
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Order Saga for Order {OrderId}", State.OrderId);

        // Step 1: Reserve Inventory
        var inventoryItems = new List<InventoryItem>(); // Would be populated from order items
        var reserveInventory = new ReserveInventory(State.OrderId, inventoryItems);
        
        await PublishAsync(reserveInventory, cancellationToken);
        
        _logger.LogInformation("Inventory reservation requested for Order {OrderId}", State.OrderId);
    }

    protected override async Task OnCompensateAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Compensating Order Saga for Order {OrderId}. Reason: {Reason}", 
            State.OrderId, State.FailureReason);

        // Compensate in reverse order

        // 1. Release inventory if it was reserved
        if (State.InventoryReserved)
        {
            var inventoryItems = new List<InventoryItem>();
            var releaseInventory = new ReleaseInventory(State.OrderId, inventoryItems);
            await PublishAsync(releaseInventory, cancellationToken);
            _logger.LogInformation("Inventory released for Order {OrderId}", State.OrderId);
        }

        // 2. Refund payment if it was processed
        if (State.PaymentProcessed)
        {
            var refund = new RefundPayment(
                State.PaymentId,
                State.OrderId,
                State.TotalAmount,
                State.FailureReason ?? "Order cancelled"
            );
            await PublishAsync(refund, cancellationToken);
            _logger.LogInformation("Payment refund initiated for Order {OrderId}", State.OrderId);
        }

        // 3. Publish order failed event
        var orderFailed = new OrderFailed(
            State.OrderId,
            State.FailureReason ?? "Unknown error",
            DateTimeOffset.UtcNow
        );
        await PublishAsync(orderFailed, cancellationToken);

    }

    // Event Handlers

    public async Task HandleInventoryReserved(InventoryReserved evt, CancellationToken cancellationToken)
    {
        if (State.Status != SagaStatus.Running) return;

        _logger.LogInformation("Inventory reserved for Order {OrderId}", State.OrderId);
        
        State.InventoryReserved = true;

        // Step 2: Request Payment
        State.PaymentId = $"PAY-{Guid.NewGuid():N}";
        var requestPayment = new RequestPayment(
            State.PaymentId,
            State.OrderId,
            State.TotalAmount,
            "USD",
            new PaymentMethod("CreditCard", "****")
        );

        await PublishAsync(requestPayment, cancellationToken);
        _logger.LogInformation("Payment requested for Order {OrderId}, Payment {PaymentId}", 
            State.OrderId, State.PaymentId);
    }

    public async Task HandleInventoryReservationFailed(InventoryReservationFailed evt, CancellationToken cancellationToken)
    {
        if (State.Status != SagaStatus.Running) return;

        _logger.LogError("Inventory reservation failed for Order {OrderId}: {Reason}", 
            State.OrderId, evt.Reason);

        State.FailureReason = $"Inventory reservation failed: {evt.Reason}";
        await CompensateAsync(State.FailureReason, cancellationToken);
    }

    public async Task HandlePaymentSucceeded(PaymentSucceeded evt, CancellationToken cancellationToken)
    {
        if (State.Status != SagaStatus.Running) return;

        _logger.LogInformation("Payment succeeded for Order {OrderId}, Transaction {TransactionId}", 
            State.OrderId, evt.TransactionId);

        State.PaymentProcessed = true;

        // Step 3: Ship Order
        var shipOrder = new ShipOrder(
            State.OrderId,
            $"TRACK-{Guid.NewGuid():N}"
        );

        await PublishAsync(shipOrder, cancellationToken);
        _logger.LogInformation("Shipping requested for Order {OrderId}", State.OrderId);
    }

    public async Task HandlePaymentFailed(PaymentFailed evt, CancellationToken cancellationToken)
    {
        if (State.Status != SagaStatus.Running) return;

        _logger.LogError("Payment failed for Order {OrderId}: {Reason}", 
            State.OrderId, evt.Reason);

        State.FailureReason = $"Payment failed: {evt.Reason}";
        await CompensateAsync(State.FailureReason, cancellationToken);
    }

    public async Task HandleOrderShipped(OrderShipped evt, CancellationToken cancellationToken)
    {
        if (State.Status != SagaStatus.Running) return;

        _logger.LogInformation("Order shipped: {OrderId}, Tracking: {TrackingNumber}", 
            State.OrderId, evt.TrackingNumber);

        State.OrderShipped = true;
        State.CompletedAt = DateTimeOffset.UtcNow;

        // Publish order completed event
        var orderCompleted = new OrderCompleted(
            State.OrderId,
            State.CompletedAt.Value
        );

        await PublishAsync(orderCompleted, cancellationToken);

        // Complete the saga successfully
        await CompleteAsync(cancellationToken);
        
        _logger.LogInformation("Order Saga completed successfully for Order {OrderId}", State.OrderId);
    }

    public async Task HandleOrderCancelled(OrderCancelled evt, CancellationToken cancellationToken)
    {
        if (State.Status != SagaStatus.Running) return;

        _logger.LogInformation("Order cancelled: {OrderId}, Reason: {Reason}", 
            State.OrderId, evt.Reason);

        State.FailureReason = evt.Reason;
        await CompensateAsync(State.FailureReason, cancellationToken);
    }

    public async Task HandleCancelOrder(CancelOrder cmd, CancellationToken cancellationToken)
    {
        if (State.Status != SagaStatus.Running) return;

        _logger.LogInformation("Cancel order command received for Order {OrderId}, Reason: {Reason}", 
            State.OrderId, cmd.Reason);

        // Publish OrderCancelled event
        var orderCancelled = new OrderCancelled(State.OrderId, cmd.Reason, DateTimeOffset.UtcNow);
        await PublishAsync(orderCancelled, cancellationToken);

        // Trigger compensation via internal method
        State.FailureReason = cmd.Reason;
        await CompensateInternalAsync(cancellationToken);
    }

    private async Task CompensateInternalAsync(CancellationToken cancellationToken)
    {
        // Call the protected CompensateAsync method
        await CompensateAsync(State.FailureReason ?? "Order cancelled", cancellationToken);
    }
}
