using ECommerce.Contracts;
using ECommerce.PaymentService.Services;
using OmniFlow.Core;
using OmniFlow.Messaging;

namespace ECommerce.PaymentService.Handlers;

/// <summary>
/// Handles refund request commands
/// </summary>
public class RefundRequestHandler
{
    private readonly IPaymentProcessor _paymentProcessor;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<RefundRequestHandler> _logger;

    public RefundRequestHandler(
        IPaymentProcessor paymentProcessor,
        IMessageBus messageBus,
        ILogger<RefundRequestHandler> logger)
    {
        _paymentProcessor = paymentProcessor;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task HandleRefundPayment(MessageEnvelope<RefundPayment> envelope, MessageContext context, CancellationToken cancellationToken = default)
    {
        var request = envelope.Message;

        _logger.LogInformation("Processing refund {PaymentId} for Order {OrderId}, Amount: {Amount}",
            request.PaymentId, request.OrderId, request.Amount);

        try
        {
            // Simulate refund processing
            await Task.Delay(Random.Shared.Next(500, 1500), cancellationToken);

            var result = await _paymentProcessor.RefundPaymentAsync(
                request.PaymentId,
                request.Amount,
                cancellationToken);

            if (result)
            {
                _logger.LogInformation("Refund succeeded {PaymentId}", request.PaymentId);

                var paymentRefunded = new PaymentRefunded(
                    request.PaymentId,
                    request.OrderId,
                    request.Amount,
                    request.Reason,
                    DateTimeOffset.UtcNow
                );

                await _messageBus.PublishAsync(paymentRefunded);
            }
            else
            {
                _logger.LogWarning("Refund failed {PaymentId}", request.PaymentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund {PaymentId}", request.PaymentId);
        }
    }
}
