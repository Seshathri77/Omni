# Quick Start - eCommerce Sample

## 1. Start RabbitMQ

```bash
cd samples
docker-compose up -d
```

Verify RabbitMQ is running:
- Management UI: http://localhost:15672
- Login: guest / guest

## 2. Start Order Service

```bash
cd ECommerce.OrderService
dotnet run
```

You should see:
```
Order Service starting on Development with RabbitMQ message bus
```

Swagger UI: http://localhost:5001/swagger

## 3. Start Payment Service

```bash
cd ECommerce.PaymentService
dotnet run
```

You should see:
```
Payment Service starting on Development with RabbitMQ message bus
```

Swagger UI: http://localhost:5002/swagger

## 4. Create Test Order

```bash
curl -X POST http://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "CUST-001",
    "items": [
      {
        "productId": "LAPTOP-001",
        "productName": "Dell XPS 15",
        "quantity": 1,
        "unitPrice": 1299.99
      }
    ],
    "totalAmount": 1299.99,
    "shippingAddress": {
      "street": "123 Tech Street",
      "city": "San Francisco",
      "state": "CA",
      "zipCode": "94105",
      "country": "USA"
    }
  }'
```

## 5. Watch the Logs

### Order Service Console:
```
[OrderService] Creating order ORD-a1b2c3... for customer CUST-001
[OrderService] Starting Order Saga for Order ORD-a1b2c3...
[OrderService] Inventory reservation requested for Order ORD-a1b2c3...
[OrderService] Inventory reserved for Order ORD-a1b2c3...
[OrderService] Payment requested for Order ORD-a1b2c3..., Payment PAY-d4e5f6...
[OrderService] Payment succeeded for Order ORD-a1b2c3...
[OrderService] Shipping requested for Order ORD-a1b2c3...
[OrderService] Order shipped: ORD-a1b2c3..., Tracking: TRACK-g7h8i9...
[OrderService] Order Saga completed successfully for Order ORD-a1b2c3...
```

### Payment Service Console:
```
[PaymentService] Processing payment request PAY-d4e5f6... for Order ORD-a1b2c3..., Amount: 1299.99
[PaymentService] Simulating payment processing for PAY-d4e5f6...
[PaymentService] Payment succeeded PAY-d4e5f6..., Transaction: TXN-j1k2l3...
```

## 6. Check RabbitMQ

Visit http://localhost:15672 and navigate to:
- **Exchanges** → `ecommerce-exchange` (see message routing)
- **Queues** → Various message queues (see message flow)
- **Connections** → 2 connections (OrderService + PaymentService)

## 7. Test Payment Failure

The payment processor has a 10% failure rate. Create multiple orders:

```bash
for i in {1..10}; do
  curl -X POST http://localhost:5001/api/orders \
    -H "Content-Type: application/json" \
    -d "{
      \"customerId\": \"CUST-00$i\",
      \"items\": [{
        \"productId\": \"PROD-001\",
        \"productName\": \"Test Product\",
        \"quantity\": 1,
        \"unitPrice\": 99.99
      }],
      \"totalAmount\": 99.99,
      \"shippingAddress\": {
        \"street\": \"123 St\",
        \"city\": \"SF\",
        \"state\": \"CA\",
        \"zipCode\": \"94105\",
        \"country\": \"USA\"
      }
    }"
done
```

Watch for compensation logs:
```
[PaymentService] Payment failed PAY-xyz...: Insufficient funds or card declined
[OrderService] Payment failed for Order ORD-abc...: Insufficient funds or card declined
[OrderService] Compensating Order Saga for Order ORD-abc...
[OrderService] Inventory released for Order ORD-abc...
```

## 8. Test Order Cancellation

```bash
# Get order ID from previous response
ORDER_ID="ORD-abc123..."

curl -X POST http://localhost:5001/api/orders/$ORDER_ID/cancel \
  -H "Content-Type: application/json" \
  -d '{"reason": "Customer changed mind"}'
```

## 9. Switch to InMemory (Development)

Edit `appsettings.Development.json`:
```json
{
  "MessageBus": {
    "Provider": "InMemory"
  }
}
```

Restart services - they'll now use in-memory messaging (no RabbitMQ required).

## 10. Stop Everything

```bash
# Stop services: Ctrl+C in terminals

# Stop RabbitMQ:
cd samples
docker-compose down
```

## Next Steps

- Read the full [README.md](README.md) for architecture details
- Explore the saga code in `ECommerce.OrderService/Sagas/OrderSaga.cs`
- Try switching to Kafka or Azure Service Bus
- Add more services (Inventory, Shipping)

## Troubleshooting

**Error: "Connection to RabbitMQ failed"**
→ Make sure RabbitMQ is running: `docker ps | grep rabbitmq`

**Error: "Port 5001 already in use"**
→ Change port in `launchSettings.json` or kill the process

**No messages processed**
→ Check both services are running and check RabbitMQ queues

**Payment always succeeds**
→ Create 10+ orders to see ~10% failure rate

---

**Need help?** Check the full [README.md](README.md) or the [main OmniFlow documentation](../../README.md)
