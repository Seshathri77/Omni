using ECommerce.Contracts;
using ECommerce.OrderService.Sagas;
using Microsoft.AspNetCore.Mvc;
using OmniFlow.Messaging;
using OmniFlow.Sagas;

namespace ECommerce.OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IMessageBus _messageBus;
    private readonly ISagaRepository<OrderSagaState> _sagaRepository;
    private readonly ITimerService _timerService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IMessageBus messageBus,
        ISagaRepository<OrderSagaState> sagaRepository,
        ITimerService timerService,
        IServiceProvider serviceProvider,
        ILogger<OrdersController> logger)
    {
        _messageBus = messageBus;
        _sagaRepository = sagaRepository;
        _timerService = timerService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var orderId = $"ORD-{Guid.NewGuid():N}";

        _logger.LogInformation("Creating order {OrderId} for customer {CustomerId}", 
            orderId, request.CustomerId);

        // Create initial saga state
        // CorrelationId is automatically set by the saga framework from ICorrelationAccessor
        var initialState = new OrderSagaState
        {
            OrderId = orderId,
            CustomerId = request.CustomerId,
            TotalAmount = request.TotalAmount
        };

        // Create Order Saga
        var saga = _serviceProvider.GetRequiredService<OrderSaga>();
        saga.Initialize(_sagaRepository, _messageBus, _timerService);

        // Start the saga - CorrelationId is set automatically
        await saga.StartWithStateAsync(initialState);

        // Publish OrderCreated event - MessageEnvelope adds CorrelationId automatically
        var orderCreated = new OrderCreated(
            orderId,
            request.CustomerId,
            request.Items,
            request.TotalAmount,
            request.ShippingAddress,
            DateTimeOffset.UtcNow
        );

        await _messageBus.PublishAsync(orderCreated);

        return Ok(new
        {
            OrderId = orderId,
            Status = "Processing",
            Message = "Order created and saga started"
        });
    }

    [HttpPost("{orderId}/cancel")]
    public async Task<IActionResult> CancelOrder(string orderId, [FromBody] CancelOrderRequest request)
    {
        _logger.LogInformation("Cancelling order {OrderId}", orderId);

        var cancelOrder = new CancelOrder(orderId, request.Reason);
        await _messageBus.PublishAsync(cancelOrder);

        return Ok(new { Message = "Order cancellation requested" });
    }

    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrderStatus(string orderId)
    {
        // In a real application, you would query a read model or database
        // For demo purposes, we'll return a simple response
        return Ok(new
        {
            OrderId = orderId,
            Status = "Processing",
            Message = "Use saga ID to track detailed status"
        });
    }
}

public record CreateOrderRequest(
    string CustomerId,
    List<OrderItem> Items,
    decimal TotalAmount,
    ShippingAddress ShippingAddress
);

public record CancelOrderRequest(string Reason);
