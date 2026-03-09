using ECommerce.Contracts;
using ECommerce.PaymentService.Services;
using OmniFlow.Core;
using OmniFlow.Messaging;

namespace ECommerce.PaymentService.Handlers;

/// <summary>
/// Handles payment request commands
/// </summary>
public class PaymentRequestHandler
{
    private readonly IPaymentProcessor _paymentProcessor;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<PaymentRequestHandler> _logger;

    public PaymentRequestHandler(
        IPaymentProcessor paymentProcessor,
        IMessageBus messageBus,
        ILogger<PaymentRequestHandler> logger)
    {
        _paymentProcessor = paymentProcessor;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task HandleRequestPayment(MessageEnvelope<RequestPayment> envelope, MessageContext context, CancellationToken cancellationToken = default)
    {
        var request = envelope.Message;

        _logger.LogInformation("Processing payment request {PaymentId} for Order {OrderId}, Amount: {Amount}",
            request.PaymentId, request.OrderId, request.Amount);

        try
        {
            // Simulate payment processing delay
            await Task.Delay(Random.Shared.Next(1000, 3000), cancellationToken);

            // Process payment
            var result = await _paymentProcessor.ProcessPaymentAsync(
                request.PaymentId,
                request.Amount,
                request.Currency,
                request.PaymentMethod,
                cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Payment succeeded {PaymentId}, Transaction: {TransactionId}",
                    request.PaymentId, result.TransactionId);

                var paymentSucceeded = new PaymentSucceeded(
                    request.PaymentId,
                    request.OrderId,
                    request.Amount,
                    result.TransactionId!,
                    DateTimeOffset.UtcNow
                );

                await _messageBus.PublishAsync(paymentSucceeded);
            }
            else
            {
                _logger.LogWarning("Payment failed {PaymentId}: {Reason}",
                    request.PaymentId, result.FailureReason);

                var paymentFailed = new PaymentFailed(
                    request.PaymentId,
                    request.OrderId,
                    request.Amount,
                    result.FailureReason ?? "Payment processing failed",
                    DateTimeOffset.UtcNow
                );

                await _messageBus.PublishAsync(paymentFailed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment {PaymentId}", request.PaymentId);

            var paymentFailed = new PaymentFailed(
                request.PaymentId,
                request.OrderId,
                request.Amount,
                ex.Message,
                DateTimeOffset.UtcNow
            );

            await _messageBus.PublishAsync(paymentFailed);
        }
    }
}
