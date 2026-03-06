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
│   ├── OmniFlow.Core                  # Core primitives (correlation, envelope, context)
│   ├── OmniFlow.Messaging             # Message bus abstraction + middleware pipeline
│   ├── OmniFlow.Sagas                 # Saga orchestration engine
│   ├── OmniFlow.Idempotency           # Idempotency store abstractions
│   ├── OmniFlow.Observability         # OpenTelemetry & Serilog integration
│   ├── OmniFlow.Adapters.RabbitMQ     # RabbitMQ message bus implementation
│   ├── OmniFlow.Adapters.Kafka        # Apache Kafka message bus implementation
│   ├── OmniFlow.Adapters.AzureServiceBus  # Azure Service Bus implementation
│   ├── OmniFlow.Adapters.Sql          # SQL-based persistence adapters
│   ├── OmniFlow.Adapters.MongoDb      # MongoDB persistence adapters
│   └── OmniFlow.Tools.Cli             # CLI for saga inspection
├── samples/
│   ├── OrdersService                  # Example: Order processing with saga
│   └── PaymentsService                # Example: Payment processing service
└── tests/
    └── OmniFlow.Tests                 # Unit tests for all components
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

OmniFlow provides multiple message bus adapters for different scenarios:

### In-Memory (Development)

Default adapter for development and testing:
```csharp
services.AddOmniFlowMessaging(); // Uses InMemoryMessageBus
```

### RabbitMQ (Production)

Enterprise messaging with RabbitMQ:
```csharp
services.AddRabbitMQMessageBus(options =>
{
    options.HostName = "localhost";
    options.ExchangeName = "omniflow";
    options.ServiceName = "my-service";
});
```

**Best for**: Traditional microservices, reliable message delivery

[📖 RabbitMQ Adapter Documentation](src/OmniFlow.Adapters.RabbitMQ/README.md)

### Apache Kafka (High Throughput)

High-performance event streaming with Kafka:
```csharp
services.AddKafkaMessageBus(options =>
{
    options.BootstrapServers = "localhost:9092";
    options.ConsumerGroupId = "my-service-group";
    options.TopicPrefix = "prod";
});
```

**Best for**: Event streaming, high-volume messaging, event sourcing

[📖 Kafka Adapter Documentation](src/OmniFlow.Adapters.Kafka/README.md)

### Azure Service Bus (Cloud-Native)

Fully managed Azure messaging with advanced features:
```csharp
services.AddAzureServiceBusMessageBus(options =>
{
    options.FullyQualifiedNamespace = "mybus.servicebus.windows.net";
    options.TopicName = "omniflow";
    options.EnableSessions = true; // For ordered processing
});
```

**Best for**: Azure cloud deployments, session-based ordering, managed infrastructure

[📖 Azure Service Bus Adapter Documentation](src/OmniFlow.Adapters.AzureServiceBus/README.md)

### SQL Persistence

Store saga state and idempotency records in SQL:
```csharp
services.AddOmniFlowSqlAdapters(connectionString);
```

### MongoDB Persistence

NoSQL alternative for saga state storage:
```csharp
services.AddMongoDbSagaRepository(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "omniflow";
});
```

### Adapter Comparison

| Adapter | Throughput | Durability | Ordering | Use Case |
|---------|------------|------------|----------|----------|
| In-Memory | Very High | ❌ None | ✅ | Development, Testing |
| RabbitMQ | Medium | ✅ Disk | ⚠️ Per Queue | Microservices |
| Kafka | Very High | ✅ Replicated | ✅ Per Partition | Event Streaming |
| Azure Service Bus | High | ✅ Cloud | ✅ Sessions | Azure Cloud |

## 🎯 Production-Ready Features

OmniFlow includes critical features required for production deployments:

### Saga Timeouts & Durable Timers

Prevent sagas from getting stuck indefinitely using durable timers that survive service restarts:

```csharp
public class OrderSaga : Saga<OrderSagaState>
{
    protected override async Task OnStartAsync(CancellationToken ct)
    {
        // Schedule timeout - saga will receive SagaTimerFired event after 30 minutes
        var timerId = await ScheduleTimerAsync(
            TimeSpan.FromMinutes(30), 
            "PaymentTimeout", 
            ct);
        
        State.PaymentTimeoutId = timerId;
        
        await PublishAsync(new RequestPayment(State.OrderId, State.Amount), ct);
    }

    public async Task HandlePaymentSucceeded(PaymentSucceeded evt, CancellationToken ct)
    {
        // Cancel timeout since payment succeeded
        await CancelTimerAsync(State.PaymentTimeoutId, ct);
        await CompleteAsync(ct);
    }

    public async Task HandleSagaTimerFired(SagaTimerFired evt, CancellationToken ct)
    {
        if (evt.TimerName == "PaymentTimeout")
        {
            await CompensateAsync("Payment timed out after 30 minutes", ct);
        }
    }
}
```

**Configuration:**
```csharp
// Add SQL-based durable timer service (survives restarts)
services.AddOmniFlowSqlAdapters(connectionString);
services.AddSqlTimerService();
```

### Distributed Locks

Prevent duplicate saga starts across multiple service instances:

```csharp
public class OrderSaga : Saga<OrderSagaState>
{
    private IDistributedLock _lock = null!;

    public void Initialize(ISagaRepository<OrderSagaState> repository,
                          IMessageBus messageBus,
                          IDistributedLock distributedLock)
    {
        base.Initialize(repository, messageBus);
        _lock = distributedLock;
    }

    public async Task HandleOrderCreated(OrderCreated evt, CancellationToken ct)
    {
        // Acquire lock with 5-minute timeout
        await using var lockHandle = await _lock.AcquireAsync(
            $"saga:order:{evt.OrderId}", 
            TimeSpan.FromMinutes(5), 
            ct);

        if (lockHandle == null)
        {
            // Another instance is already processing this order
            return;
        }

        // Start saga - lock released automatically on dispose
        if (!await LoadAsync(evt.OrderId, ct))
        {
            await StartAsync(evt.OrderId, ct);
        }
    }
}
```

**Configuration:**
```csharp
// SQL-based distributed lock (multi-instance coordination)
services.AddSqlDistributedLock();

// OR in-memory for development
services.AddInMemoryDistributedLock();
```

### Dead Letter Queue Processing

Automatically retry failed messages with exponential backoff:

```csharp
// Register DLQ processor
services.AddDeadLetterQueueProcessor(options =>
{
    options.MaxRetries = 3;
    options.InitialRetryDelay = TimeSpan.FromMinutes(1);
    options.MaxRetryDelay = TimeSpan.FromHours(1);
    options.AlertWebhookUrl = "https://alerts.mycompany.com/webhook";
});

// Store DLQ messages in SQL
services.AddOmniFlowSqlAdapters(connectionString);
```

The DLQ processor:
- ✅ Automatically retries failed messages with exponential backoff (1m → 5m → 15m → 1h)
- ✅ Tracks retry attempts and failure reasons
- ✅ Sends alerts when messages exhaust all retries
- ✅ Publishes `DeadLetterMessageExhaustionAlert` events for monitoring

**Message Flow:**
```
Message Fails → DLQ Store → Retry #1 (after 1 min)
                    ↓
                Retry #2 (after 5 min)
                    ↓
                Retry #3 (after 15 min)
                    ↓
            Exhausted → Alert Webhook
```

### Database Schema

Apply migrations for critical features:

```bash
# Generate migration (if using EF CLI)
dotnet ef migrations add AddCriticalFeatures --project src/OmniFlow.Adapters.Sql

# Or use the pre-created migration
dotnet ef database update --project src/OmniFlow.Adapters.Sql
```

**Tables Created:**
- `SagaTimers` - Durable timer storage
- `DistributedLocks` - Lock coordination records  
- `DeadLetterQueue` - Failed message storage and retry metadata

### Production Configuration Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// Core services
builder.Services.AddOmniFlowCore();
builder.Services.AddOmniFlowMessaging();
builder.Services.AddOmniFlowSagas();
builder.Services.AddOmniFlowObservability("OrdersService");

// Production persistence
var sqlConnection = builder.Configuration.GetConnectionString("OmniFlow");
builder.Services.AddOmniFlowSqlAdapters(sqlConnection);

// Critical production features
builder.Services.AddSqlTimerService();           // Saga timeouts
builder.Services.AddSqlDistributedLock();        // Multi-instance coordination
builder.Services.AddDeadLetterQueueProcessor();  // Failed message retry

// Message bus adapter
builder.Services.AddRabbitMQMessageBus(opts => 
{
    opts.HostName = "rabbitmq.production.local";
    opts.ExchangeName = "omniflow-prod";
});

// Register sagas
builder.Services.AddSaga<OrderSaga, OrderSagaState>();

var app = builder.Build();

// Subscribe to events
var messageBus = app.Services.GetRequiredService<IMessageBus>();
await messageBus.SubscribeAsync<OrderCreated>(async (envelope, ctx) =>
{
    using var scope = app.Services.CreateScope();
    var saga = scope.ServiceProvider.GetRequiredService<OrderSaga>();
    var repository = scope.ServiceProvider.GetRequiredService<ISagaRepository<OrderSagaState>>();
    var distributedLock = scope.ServiceProvider.GetRequiredService<IDistributedLock>();
    
    saga.Initialize(repository, messageBus, distributedLock);
    await saga.HandleOrderCreated(envelope.Message, ctx.CancellationToken);
});

await app.RunAsync();
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
