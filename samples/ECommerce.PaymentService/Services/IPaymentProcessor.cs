using ECommerce.Contracts;

namespace ECommerce.PaymentService.Services;

/// <summary>
/// Payment processor interface
/// </summary>
public interface IPaymentProcessor
{
    Task<PaymentResult> ProcessPaymentAsync(
        string paymentId,
        decimal amount,
        string currency,
        PaymentMethod paymentMethod,
        CancellationToken cancellationToken);

    Task<bool> RefundPaymentAsync(
        string paymentId,
        decimal amount,
        CancellationToken cancellationToken);
}

/// <summary>
/// Payment processing result
/// </summary>
public class PaymentResult
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string? FailureReason { get; set; }
}
