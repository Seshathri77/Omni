using Microsoft.AspNetCore.Mvc;
using OmniFlow.Core;
using OmniFlow.Messaging;
using OmniFlow.Sagas;
using OrdersService.Messages;
using OrdersService.Sagas;

namespace OrdersService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IMessageBus _messageBus;
    private readonly ISagaRepository<OrderSagaState> _sagaRepository;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IMessageBus messageBus,
        ISagaRepository<OrderSagaState> sagaRepository,
        ILogger<OrdersController> logger)
    {
        _messageBus = messageBus;
        _sagaRepository = sagaRepository;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var orderId = Guid.NewGuid().ToString();
        
        var orderCreated = new OrderCreated(orderId, request.Amount, request.CustomerId);
        await _messageBus.PublishAsync(orderCreated);

        _logger.LogInformation("Created order {OrderId} for customer {CustomerId}", 
            orderId, request.CustomerId);

        return Accepted(new { OrderId = orderId });
    }

    [HttpPost("test-duplicate/{orderId}")]
    public async Task<IActionResult> TestDuplicateMessage(string orderId)
    {
        // Publish the same OrderCreated message twice to test idempotency
        var orderCreated = new OrderCreated(orderId, 99.99m, "test-customer");
        
        _logger.LogWarning("Testing idempotency: Publishing message 1");
        await _messageBus.PublishAsync(orderCreated);
        
        await Task.Delay(100); // Small delay
        
        _logger.LogWarning("Testing idempotency: Publishing message 2 (duplicate)");
        await _messageBus.PublishAsync(orderCreated);

        return Ok(new { Message = "Published duplicate messages. Check logs for idempotency behavior." });
    }

    [HttpGet("idempotency/check/{messageId}")]
    public async Task<IActionResult> CheckIdempotency(string messageId, [FromServices] OmniFlow.Idempotency.IIdempotencyStore idempotencyStore)
    {
        var exists = await idempotencyStore.ExistsAsync(messageId, "OrdersService-OrderCreated");
        return Ok(new { MessageId = messageId, AlreadyProcessed = exists });
    }

    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrderStatus(string orderId)
    {
        var result = await _sagaRepository.GetAsync(orderId);
        
        if (result == null)
            return NotFound();

        return Ok(new
        {
            OrderId = result.Value.State.OrderId,
            Status = result.Value.State.Status.ToString(),
            Amount = result.Value.State.Amount,
            PaymentCompleted = result.Value.State.PaymentCompleted,
            History = result.Value.State.History
        });
    }
}

public record CreateOrderRequest(decimal Amount, string CustomerId);
