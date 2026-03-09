using ECommerce.Contracts;

namespace ECommerce.PaymentService.Services;

/// <summary>
/// Simulated payment processor for demonstration purposes
/// In production, this would integrate with a real payment gateway (Stripe, PayPal, etc.)
/// </summary>
public class SimulatedPaymentProcessor : IPaymentProcessor
{
    private readonly ILogger<SimulatedPaymentProcessor> _logger;
    private readonly Dictionary<string, string> _processedPayments = new();

    public SimulatedPaymentProcessor(ILogger<SimulatedPaymentProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(
        string paymentId,
        decimal amount,
        string currency,
        PaymentMethod paymentMethod,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Simulating payment processing for {PaymentId}", paymentId);

        await Task.Delay(100, cancellationToken); // Simulate API call

        // Simulate 90% success rate
        var success = Random.Shared.Next(100) < 90;

        if (success)
        {
            var transactionId = $"TXN-{Guid.NewGuid():N}";
            _processedPayments[paymentId] = transactionId;

            return new PaymentResult
            {
                Success = true,
                TransactionId = transactionId
            };
        }

        return new PaymentResult
        {
            Success = false,
            FailureReason = "Insufficient funds or card declined"
        };
    }

    public async Task<bool> RefundPaymentAsync(
        string paymentId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Simulating refund for {PaymentId}", paymentId);

        await Task.Delay(100, cancellationToken); // Simulate API call

        if (_processedPayments.ContainsKey(paymentId))
        {
            _processedPayments.Remove(paymentId);
            return true;
        }

        return false;
    }
}
