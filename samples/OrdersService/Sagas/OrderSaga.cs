using OmniFlow.Sagas;
using OrdersService.Messages;

namespace OrdersService.Sagas;

/// <summary>
/// State for the order saga.
/// </summary>
public class OrderSagaState : SagaState
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string? PaymentId { get; set; }
    public bool PaymentRequested { get; set; }
    public bool PaymentCompleted { get; set; }
}

/// <summary>
/// Orchestration saga for order processing with compensation.
/// Workflow: OrderCreated → PaymentRequested → PaymentSucceeded → OrderCompleted
/// Compensation: PaymentFailed → CancelOrder
/// </summary>
public class OrderSaga : Saga<OrderSagaState>
{
    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        // State properties are set before this is called
        await PublishAsync(
            new PaymentRequested(State.OrderId, State.Amount),
            cancellationToken);
        
        State.PaymentRequested = true;
    }

    public async Task StartOrderAsync(OrderCreated orderCreated, CancellationToken cancellationToken)
    {
        // Create initial state with order data
        // Use OrderId as SagaId so we can look up saga by order ID
        var initialState = new OrderSagaState
        {
            SagaId = orderCreated.OrderId, // Use OrderId as SagaId for easy lookup
            CorrelationId = orderCreated.OrderId,
            OrderId = orderCreated.OrderId,
            Amount = orderCreated.Amount,
            CustomerId = orderCreated.CustomerId
        };
        
        // Start saga with pre-populated state
        await StartAsync(initialState, cancellationToken);
    }

    public async Task HandlePaymentSucceededAsync(PaymentSucceeded paymentSucceeded, CancellationToken cancellationToken)
    {
        if (State.Status != SagaStatus.Running)
            return;

        State.PaymentId = paymentSucceeded.PaymentId;
        State.PaymentCompleted = true;

        await PublishAsync(
            new OrderCompleted(State.OrderId),
            cancellationToken);

        await CompleteAsync(cancellationToken);
    }

    public async Task HandlePaymentFailedAsync(PaymentFailed paymentFailed, CancellationToken cancellationToken)
    {
        if (State.Status != SagaStatus.Running)
            return;

        await CompensateAsync($"Payment failed: {paymentFailed.Reason}", cancellationToken);
    }

    protected override async Task OnCompensateAsync(CancellationToken cancellationToken)
    {
        // Compensation logic: cancel the order
        await PublishAsync(
            new OrderCancelled(State.OrderId, "Payment processing failed"),
            cancellationToken);
    }
}
