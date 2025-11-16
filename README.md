# OmniFlow.Framework

A comprehensive .NET 8 framework for building resilient, observable microservices with built-in support for saga orchestration, distributed tracing, and idempotent message processing.

[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## 🚀 Features

- **🔗 Correlation & Distributed Tracing**: Automatic correlation ID propagation with OpenTelemetry/Jaeger
- **🔄 Saga Orchestration**: Durable saga engine with compensation logic for distributed transactions
- **📨 Pluggable Message Bus**: Abstract message bus with in-memory and RabbitMQ adapters
- **✅ Idempotency**: Built-in middleware to prevent duplicate message processing
- **📊 Observability**: OpenTelemetry traces, Prometheus metrics, and Serilog structured logging
- **🛡️ Resilience**: Polly-based retry policies and circuit breakers
- **📦 Schema Evolution**: Message versioning support for schema migration
- **🔐 Message Signing**: HMAC-based message authentication and validation

## 📖 Documentation

**👉 [Complete Documentation](DOCUMENTATION.md)** - Comprehensive guide covering:
- Quick start and setup
- Testing with RabbitMQ
- PaymentsService API reference
- Observability with Jaeger, Seq, Prometheus, and Grafana
- Production deployment
- Troubleshooting

## Project Structure

```
OmniFlow.sln
├── src/
│   ├── OmniFlow.Core              # Core primitives (correlation, envelope, context)
│   ├── OmniFlow.Messaging         # Message bus abstraction + middleware pipeline
│   ├── OmniFlow.Sagas             # Saga orchestration engine
│   ├── OmniFlow.Idempotency       # Idempotency store abstractions
│   ├── OmniFlow.Observability     # OpenTelemetry & Serilog integration
│   ├── OmniFlow.Adapters.RabbitMQ # RabbitMQ message bus implementation
│   ├── OmniFlow.Adapters.Sql      # SQL-based persistence adapters
│   └── OmniFlow.Tools.Cli         # CLI for saga inspection
├── samples/
│   ├── OrdersService              # Example: Order processing with saga
│   └── PaymentsService            # Example: Payment processing service
└── tests/
    └── OmniFlow.Tests             # Unit tests for all components
```

## Quick Start

### 1. Install NuGet Packages

```bash
dotnet add package OmniFlow.Core
dotnet add package OmniFlow.Messaging
dotnet add package OmniFlow.Sagas
dotnet add package OmniFlow.Observability
```

### 2. Configure Services

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add OmniFlow services
builder.Services.AddOmniFlowCore();
builder.Services.AddOmniFlowMessaging();
builder.Services.AddOmniFlowSagas();
builder.Services.AddOmniFlowIdempotency();
builder.Services.AddOmniFlowObservability("MyService");

// Register your sagas
builder.Services.AddSaga<OrderSaga, OrderSagaState>();
```

### 3. Define Messages

```csharp
using OmniFlow.Core;

public record OrderCreated(string OrderId, decimal Amount) : IEvent;
public record PaymentRequested(string OrderId, decimal Amount) : ICommand;
```

### 4. Create a Saga

```csharp
using OmniFlow.Sagas;

public class OrderSagaState : SagaState
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class OrderSaga : Saga<OrderSagaState>
{
    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        // Publish command to request payment
        await PublishAsync(
            new PaymentRequested(State.OrderId, State.Amount),
            cancellationToken);
    }

    protected override async Task OnCompensateAsync(CancellationToken cancellationToken)
    {
        // Compensation logic (rollback)
        await PublishAsync(new CancelOrder(State.OrderId), cancellationToken);
    }
}
```

### 5. Publish and Subscribe

```csharp
var messageBus = app.Services.GetRequiredService<IMessageBus>();

// Subscribe to events
await messageBus.SubscribeAsync<OrderCreated>(async (envelope, context) =>
{
    var saga = new OrderSaga();
    saga.Initialize(repository, messageBus);
    await saga.StartAsync(envelope.CorrelationId);
});

// Publish messages
await messageBus.PublishAsync(new OrderCreated("order-123", 99.99m));
```

## Key Concepts

### Message Envelope

Every message is wrapped in a `MessageEnvelope<T>` containing:
- `MessageId`: Unique message identifier
- `CorrelationId`: For distributed tracing
- `CausationId`: ID of the message that caused this one
- `Timestamp`: Message creation time
- `SchemaVersion`: For message evolution
- `Signature`: Optional HMAC signature

### Middleware Pipeline

Messages flow through a configurable middleware pipeline:
1. **CorrelationMiddleware**: Sets correlation context
2. **LoggingMiddleware**: Logs message processing
3. **RetryMiddleware**: Polly-based retry logic

### Saga Patterns

**Orchestration**: Central saga coordinates all steps
```csharp
public class OrderSaga : Saga<OrderSagaState>
{
    // Saga controls the entire workflow
}
```

**Choreography**: Services react to events independently (use message bus subscriptions)

### Idempotency

Ensure messages are processed exactly once:
```csharp
var idempotencyStore = services.GetRequiredService<IIdempotencyStore>();

if (await idempotencyStore.TryRecordAsync(messageId, "MyConsumer"))
{
    // Process message (first time)
}
// Else: already processed
```

## Observability

### Distributed Tracing

All operations create OpenTelemetry spans:
```csharp
using var activity = OmniFlowTelemetry.StartMessageActivity(envelope);
// Process message
```

### Metrics

Built-in metrics for monitoring:
- `omniflow.messages.published`
- `omniflow.messages.processed`
- `omniflow.sagas.started`
- `omniflow.sagas.completed`

### Structured Logging

Correlation IDs automatically added to logs:
```
[10:23:45 INF] [correlation-123] Processing OrderCreated for order-456
```

## Adapters

### RabbitMQ

```csharp
services.AddRabbitMQMessageBus(options =>
{
    options.HostName = "localhost";
    options.ExchangeName = "omniflow";
});
```

### SQL Persistence

```csharp
services.AddOmniFlowSqlAdapters(connectionString);
```

## Testing

Run tests:
```bash
dotnet test
```

Example test:
```csharp
[Fact]
public async Task Should_Complete_Saga_Successfully()
{
    var repository = new InMemorySagaRepository<OrderSagaState>();
    var saga = new OrderSaga();
    saga.Initialize(repository, messageBus);
    
    await saga.StartAsync("correlation-123");
    
    var state = await repository.GetAsync(saga.State.SagaId);
    state.Should().NotBeNull();
}
```

## CLI Tool

Inspect and manage sagas:
```bash
omniflow-cli list --connection "Server=..."
omniflow-cli inspect --saga-id abc123 --connection "Server=..."
omniflow-cli replay --saga-id abc123 --connection "Server=..."
```

## 🎯 Quick Links

- **[Complete Documentation](DOCUMENTATION.md)** - Full guide with examples
- **[Sample Services](samples/)** - OrdersService and PaymentsService examples
- **[Tests](tests/OmniFlow.Tests/)** - Unit and integration tests

## 🏃 Quick Start

```bash
# 1. Start observability stack
docker-compose -f docker-compose-observability.yml up -d

# 2. Run sample services
cd samples/OrdersService && dotnet run
cd samples/PaymentsService && dotnet run  # In separate terminal

# 3. Create an order
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"amount": 99.99, "customerId": "cust-123"}'

# 4. Monitor the system
# Logs: http://localhost:5341 (Seq)
# Traces: http://localhost:16686 (Jaeger)
# Metrics: http://localhost:9090 (Prometheus)
# Messages: http://localhost:15672 (RabbitMQ)
```

## 🔧 Sample Services

- **OrdersService** (Port 5000): Saga orchestrator for order processing
- **PaymentsService** (Port 5001): Payment processing with REST API

See [DOCUMENTATION.md](DOCUMENTATION.md#testing-with-rabbitmq) for detailed testing scenarios.

## 🏗️ Architecture

```
┌─────────────────┐    RabbitMQ     ┌──────────────────┐
│ OrdersService   │ ──────────────> │ PaymentsService  │
│ (Orchestrator)  │ <────────────── │ (Participant)    │
└─────────────────┘                 └──────────────────┘
        ↓                                    ↓
    OrderSaga                          Processes
    Coordinates                        Payments
        ↓                                    ↓
┌────────────────────────────────────────────────────┐
│           Observability Stack                      │
│  Jaeger (Traces) | Seq (Logs) | Prometheus (Metrics) │
└────────────────────────────────────────────────────┘
```

## 🤝 Contributing

Contributions welcome! This framework follows:
- Clean code principles & SOLID design patterns
- Comprehensive XML documentation
- Test-driven development

## 📄 License

MIT

## 🔗 Resources

- [Complete Documentation](DOCUMENTATION.md)
- [Saga Pattern](https://microservices.io/patterns/data/saga.html)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
