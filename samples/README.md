# eCommerce Sample - OmniFlow Saga Orchestration

This sample demonstrates a real-world eCommerce order fulfillment system using OmniFlow's saga orchestration pattern with RabbitMQ message bus.

## 🚀 Quick Start

### Option 1: .NET Aspire (Recommended for Development)

**One command starts everything:**

```bash
cd ECommerce.AppHost
dotnet run
```

Aspire dashboard opens at **https://localhost:17000** showing all services, traces, logs, and metrics!

### Option 2: Docker Compose

```bash
docker-compose up -d
cd ECommerce.OrderService && dotnet run  # Terminal 1
cd ECommerce.PaymentService && dotnet run  # Terminal 2
```

👉 **See [ASPIRE_INTEGRATION.md](ASPIRE_INTEGRATION.md) for complete Aspire guide**

## Architecture Overview

The sample consists of two microservices:

### 1. **Order Service** (Port 5001)
- Orchestrates the order fulfillment saga
- Coordinates inventory reservation, payment, and shipping
- Implements compensating transactions for failures
- Exposes REST API for order creation and management

### 2. **Payment Service** (Port 5002)
- Processes payment requests
- Simulates payment gateway integration
- Handles payment failures and refunds
- Publishes payment events

## Saga Flow

```
┌─────────────┐
│Create Order │
└──────┬──────┘
       │
       v
┌──────────────────┐
│ Reserve Inventory│
└──────┬───────────┘
       │
       v
  ┌────┴────┐
  │Success? │
  └────┬────┘
       │
       ├─── No ──> Fail & Compensate
       │
       v Yes
┌──────────────┐
│Process Payment│
└──────┬───────┘
       │
       v
  ┌────┴────┐
  │Success? │
  └────┬────┘
       │
       ├─── No ──> Refund & Compensate
       │
       v Yes
┌──────────────┐
│  Ship Order  │
└──────┬───────┘
       │
       v
┌──────────────┐
│   Complete   │
└──────────────┘
```

## Prerequisites

- .NET 8 SDK
- Docker Desktop (for RabbitMQ)
- Your favorite API client (Postman, curl, etc.)

## Running the Sample

### 1. Start RabbitMQ

Using Docker:

```bash
docker-compose up -d
```

Or manually:

```bash
docker run -d --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  rabbitmq:3-management
```

Access RabbitMQ Management UI: http://localhost:15672 (guest/guest)

### 2. Start the Services

**Terminal 1 - Order Service:**
```bash
cd samples/ECommerce.OrderService
dotnet run
```

**Terminal 2 - Payment Service:**
```bash
cd samples/ECommerce.PaymentService
dotnet run
```

### 3. Test the Flow

**Create an Order:**

```bash
curl -X POST http://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "CUST-001",
    "items": [
      {
        "productId": "PROD-123",
        "productName": "Laptop",
        "quantity": 1,
        "unitPrice": 999.99
      }
    ],
    "totalAmount": 999.99,
    "shippingAddress": {
      "street": "123 Main St",
      "city": "San Francisco",
      "state": "CA",
      "zipCode": "94102",
      "country": "USA"
    }
  }'
```

**Response:**
```json
{
  "orderId": "ORD-abc123...",
  "sagaId": "saga-xyz789...",
  "status": "Processing",
  "message": "Order created and saga started"
}
```

**Cancel an Order:**

```bash
curl -X POST http://localhost:5001/api/orders/{orderId}/cancel \
  -H "Content-Type: application/json" \
  -d '{
    "reason": "Customer requested cancellation"
  }'
```

## Configuration

### Switch Message Bus Provider

**Development (InMemory):**
```json
{
  "MessageBus": {
    "Provider": "InMemory"
  }
}
```

**Production (RabbitMQ):**
```json
{
  "MessageBus": {
    "Provider": "RabbitMQ",
    "RabbitMQ": {
      "HostName": "localhost",
      "Port": 5672,
      "UserName": "guest",
      "Password": "guest",
      "VirtualHost": "/"
    }
  }
}
```

### Environment Variables

Set via `appsettings.{Environment}.json` or environment variables:

```bash
export MessageBus__Provider=RabbitMQ
export MessageBus__RabbitMQ__HostName=rabbitmq.mycompany.com
```

## Observing the Saga

### 1. **Console Logs**

Watch the correlated log messages across services:

```
[Order Service] Creating order ORD-123 for customer CUST-001
[Order Service] Starting Order Saga for Order ORD-123
[Order Service] Inventory reservation requested for Order ORD-123
[Order Service] Inventory reserved for Order ORD-123
[Order Service] Payment requested for Order ORD-123, Payment PAY-456
[Payment Service] Processing payment request PAY-456 for Order ORD-123
[Payment Service] Payment succeeded PAY-456, Transaction: TXN-789
[Order Service] Payment succeeded for Order ORD-123
[Order Service] Shipping requested for Order ORD-123
[Order Service] Order shipped: ORD-123, Tracking: TRACK-xyz
[Order Service] Order Saga completed successfully for Order ORD-123
```

### 2. **RabbitMQ Management UI**

Visit http://localhost:15672 to see:
- Exchanges: `ecommerce-exchange`
- Queues: Message-specific queues
- Message flow and routing
- Dead letter queues (for failed messages)

### 3. **Swagger UI**

- Order Service: http://localhost:5001/swagger
- Payment Service: http://localhost:5002/swagger

## Key Concepts Demonstrated

### 1. **Saga Orchestration**
- `OrderSaga` coordinates multiple steps
- Automatic state persistence
- Correlation ID tracking

### 2. **Compensating Transactions**
- Inventory release on payment failure
- Payment refund on shipping failure
- Automatic rollback on saga failure

### 3. **Message-Driven Architecture**
- Commands: `CreateOrder`, `RequestPayment`
- Events: `OrderCreated`, `PaymentSucceeded`
- Loosely coupled services

### 4. **Idempotency**
- Duplicate message handling
- Retry safety
- Message deduplication

### 5. **Observability**
- Structured logging with Serilog
- Correlation ID propagation
- OpenTelemetry tracing (enabled by default)

## Testing Failure Scenarios

### 1. **Payment Failure** (90% success rate built-in)

Create multiple orders and observe some will fail payment:

```bash
for i in {1..10}; do
  curl -X POST http://localhost:5001/api/orders \
    -H "Content-Type: application/json" \
    -d "{\"customerId\":\"CUST-$i\",\"items\":[],\"totalAmount\":100,\"shippingAddress\":{}}"
done
```

Watch logs for compensation:
```
[Payment Service] Payment failed PAY-456: Insufficient funds
[Order Service] Payment failed for Order ORD-123: Insufficient funds
[Order Service] Compensating Order Saga for Order ORD-123
[Order Service] Inventory released for Order ORD-123
[Order Service] Order Saga completed with compensation
```

### 2. **Service Unavailable**

Stop the Payment Service and create an order:
```bash
# Terminal 2: Ctrl+C to stop Payment Service
```

RabbitMQ will queue the `RequestPayment` message. When Payment Service restarts, it will process queued messages.

### 3. **Order Cancellation**

```bash
curl -X POST http://localhost:5001/api/orders/ORD-123/cancel \
  -H "Content-Type: application/json" \
  -d '{"reason": "Test cancellation"}'
```

## Project Structure

```
samples/
├── ECommerce.OrderService/
│   ├── Controllers/
│   │   └── OrdersController.cs      # REST API
│   ├── Messages/
│   │   ├── OrderMessages.cs         # Order domain messages
│   │   └── IntegrationMessages.cs   # Cross-service messages
│   ├── Sagas/
│   │   ├── OrderSaga.cs             # Saga orchestration logic
│   │   └── OrderSagaState.cs        # Saga state model
│   ├── Program.cs                   # Service configuration
│   └── appsettings.json             # Configuration
│
└── ECommerce.PaymentService/
    ├── Program.cs                   # Payment processing logic
    └── appsettings.json             # Configuration
```

## Extending the Sample

### Add Inventory Service

1. Create `ECommerce.InventoryService`
2. Subscribe to `ReserveInventory` command
3. Publish `InventoryReserved` or `InventoryReservationFailed` events

### Add Shipping Service

1. Create `ECommerce.ShippingService`
2. Subscribe to `ShipOrder` command
3. Integrate with shipping provider API
4. Publish `OrderShipped` event

### Add Kafka Support

Change `appsettings.json`:

```json
{
  "MessageBus": {
    "Provider": "Kafka",
    "Kafka": {
      "BootstrapServers": "localhost:9092",
      "GroupId": "order-service-group"
    }
  }
}
```

### Add Azure Service Bus

```json
{
  "MessageBus": {
    "Provider": "ServiceBus",
    "ServiceBus": {
      "ConnectionString": "Endpoint=sb://...",
      "TopicName": "ecommerce"
    }
  }
}
```

## Troubleshooting

### RabbitMQ Connection Failed
```
Error: Connection to RabbitMQ failed
```
**Solution**: Ensure RabbitMQ is running on localhost:5672

```bash
docker ps | grep rabbitmq
```

### Messages Not Processed
```
Messages stuck in queue
```
**Solution**: Check if all services are running and subscribed

### Port Already in Use
```
Error: Port 5001 already in use
```
**Solution**: Change port in `launchSettings.json`

## Production Considerations

### 1. **Persistence**
Replace in-memory repositories with SQL/MongoDB:
```csharp
services.AddOmniFlowSqlAdapters(connectionString);
// or
services.AddOmniFlowMongoDbAdapters(connectionString, dbName);
```

### 2. **Message Broker**
Use managed RabbitMQ (CloudAMQP) or switch to Azure Service Bus/Kafka

### 3. **Monitoring**
Add Application Insights or Jaeger for distributed tracing:
```csharp
options.Observability.OtlpEndpoint = "http://jaeger:4317";
```

### 4. **Security**
- Add authentication/authorization
- Secure RabbitMQ with TLS
- Use Azure Key Vault for secrets

## Learn More

- [OmniFlow Documentation](../../README.md)
- [Saga Pattern](https://microservices.io/patterns/data/saga.html)
- [RabbitMQ Tutorials](https://www.rabbitmq.com/getstarted.html)
- [Message-Driven Architecture](https://www.enterpriseintegrationpatterns.com/)

## License

MIT License - See root LICENSE file
