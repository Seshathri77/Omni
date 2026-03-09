using OmniFlow.Sagas;

namespace ECommerce.OrderService.Sagas;

public class OrderSagaState : SagaState
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string PaymentId { get; set; } = string.Empty;
    
    // Step completion flags
    public bool InventoryReserved { get; set; }
    public bool PaymentProcessed { get; set; }
    public bool OrderShipped { get; set; }
    
    // Failure tracking
    public string? FailureReason { get; set; }
    public new DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
