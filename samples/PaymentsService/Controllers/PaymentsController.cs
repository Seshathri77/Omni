using Microsoft.AspNetCore.Mvc;
using OmniFlow.Messaging;
using OrdersService.Messages;

namespace PaymentsService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger<PaymentsController> _logger;
    private static readonly Dictionary<string, PaymentRecord> _paymentHistory = new();

    public PaymentsController(IMessageBus messageBus, ILogger<PaymentsController> logger)
    {
        _messageBus = messageBus;
        _logger = logger;
    }

    /// <summary>
    /// Manually process a payment (for testing)
    /// </summary>
    [HttpPost("process")]
    public async Task<IActionResult> ProcessPayment([FromBody] ProcessPaymentRequest request)
    {
        var paymentId = Guid.NewGuid().ToString();
        
        _logger.LogInformation("Manual payment processing for order {OrderId}, amount {Amount}", 
            request.OrderId, request.Amount);

        // Simulate processing
        await Task.Delay(500);

        if (request.ShouldSucceed)
        {
            var record = new PaymentRecord(paymentId, request.OrderId, request.Amount, "Succeeded", DateTime.UtcNow);
            _paymentHistory[request.OrderId] = record;

            await _messageBus.PublishAsync(new PaymentSucceeded(request.OrderId, paymentId));
            
            return Ok(new { PaymentId = paymentId, Status = "Succeeded", OrderId = request.OrderId });
        }
        else
        {
            var record = new PaymentRecord(paymentId, request.OrderId, request.Amount, "Failed", DateTime.UtcNow);
            _paymentHistory[request.OrderId] = record;

            await _messageBus.PublishAsync(new PaymentFailed(request.OrderId, request.FailureReason ?? "Payment declined"));
            
            return Ok(new { PaymentId = paymentId, Status = "Failed", OrderId = request.OrderId, Reason = request.FailureReason });
        }
    }

    /// <summary>
    /// Get payment history for an order
    /// </summary>
    [HttpGet("history/{orderId}")]
    public IActionResult GetPaymentHistory(string orderId)
    {
        if (_paymentHistory.TryGetValue(orderId, out var record))
        {
            return Ok(record);
        }

        return NotFound(new { Message = $"No payment history found for order {orderId}" });
    }

    /// <summary>
    /// Get all payment history
    /// </summary>
    [HttpGet("history")]
    public IActionResult GetAllPaymentHistory()
    {
        return Ok(_paymentHistory.Values.OrderByDescending(p => p.ProcessedAt));
    }

    /// <summary>
    /// Refund a payment (triggers compensation flow)
    /// </summary>
    [HttpPost("refund/{orderId}")]
    public async Task<IActionResult> RefundPayment(string orderId, [FromBody] RefundRequest request)
    {
        if (!_paymentHistory.TryGetValue(orderId, out var payment))
        {
            return NotFound(new { Message = $"No payment found for order {orderId}" });
        }

        if (payment.Status != "Succeeded")
        {
            return BadRequest(new { Message = "Can only refund successful payments" });
        }

        _logger.LogWarning("Refunding payment for order {OrderId}, reason: {Reason}", orderId, request.Reason);

        // Update local record
        _paymentHistory[orderId] = payment with { Status = "Refunded" };

        // Trigger compensation by publishing PaymentFailed
        await _messageBus.PublishAsync(new PaymentFailed(orderId, $"Refunded: {request.Reason}"));

        return Ok(new { Message = "Refund processed", OrderId = orderId });
    }

    /// <summary>
    /// Check payment service health
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            Service = "PaymentsService",
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            TotalPaymentsProcessed = _paymentHistory.Count,
            SuccessfulPayments = _paymentHistory.Values.Count(p => p.Status == "Succeeded"),
            FailedPayments = _paymentHistory.Values.Count(p => p.Status == "Failed"),
            RefundedPayments = _paymentHistory.Values.Count(p => p.Status == "Refunded")
        });
    }

    /// <summary>
    /// Clear payment history (for testing)
    /// </summary>
    [HttpDelete("history")]
    public IActionResult ClearHistory()
    {
        var count = _paymentHistory.Count;
        _paymentHistory.Clear();
        
        _logger.LogWarning("Cleared {Count} payment records", count);
        
        return Ok(new { Message = $"Cleared {count} payment records" });
    }
}

public record ProcessPaymentRequest(
    string OrderId, 
    decimal Amount, 
    bool ShouldSucceed = true, 
    string? FailureReason = null);

public record RefundRequest(string Reason);

public record PaymentRecord(
    string PaymentId, 
    string OrderId, 
    decimal Amount, 
    string Status, 
    DateTime ProcessedAt);
