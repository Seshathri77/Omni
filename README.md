# 🌊 OmniFlow Framework

[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download)
[![C#](https://img.shields.io/badge/C%23-12.0-blue.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](https://github.com/Seshathri77/Omni)

**OmniFlow** is a production-ready microservices framework for .NET 8 that provides saga orchestration, distributed tracing, and resilient message-driven architecture with enterprise-grade features.

---

## 📋 Table of Contents

- [Features](#-features)
- [Installation](#-installation)
- [Quick Start](#-quick-start)
- [Configuration](#-configuration)
- [Core Concepts](#-core-concepts)
- [Usage Examples](#-usage-examples)
- [Advanced Features](#-advanced-features)
- [Architecture](#-architecture)
- [CLI Tools](#-cli-tools)
- [Best Practices](#-best-practices)
- [Troubleshooting](#-troubleshooting)
- [Contributing](#-contributing)

---

## ✨ Features

### Core Features

- 🎯 **Saga Orchestration** - Distributed transaction management with automatic compensation
- 📨 **Message-Driven Architecture** - Pluggable message bus (InMemory, RabbitMQ, Kafka, Azure Service Bus)
- 🔄 **Outbox Pattern** - Transactional messaging with guaranteed at-least-once delivery
- 🛡️ **Idempotency** - Built-in duplicate message detection and handling
- 🔌 **Circuit Breaker** - Polly-based resilience with configurable failure thresholds
- ⚰️ **Dead Letter Queue (DLQ)** - Automatic handling of poison messages
- 📊 **Distributed Tracing** - OpenTelemetry integration with Jaeger support
- 📝 **Structured Logging** - Serilog integration with automatic correlation
- 📈 **Metrics** - Prometheus exporter for monitoring
- ⏱️ **Saga Timeouts** - Timer-based saga timeout mechanism
- 🔐 **Message Signing** - HMAC-based message authentication

### Enterprise Features

- ✅ **Production-Ready** - Optimistic concurrency, retry policies, error handling
- 🚀 **Horizontal Scaling** - Stateless services with persistent saga storage
- 🔧 **Extensible** - Adapter pattern for custom implementations
- 🎨 **Developer-Friendly** - Unified configuration with IntelliSense support
- 📦 **Modular Design** - Use only what you need
- 🧪 **Testable** - In-memory implementations for unit testing

---

## 📦 Installation

### NuGet Packages

```bash
# Core packages
dotnet add package OmniFlow.Core
dotnet add package OmniFlow.Messaging
dotnet add package OmniFlow.Sagas
dotnet add package OmniFlow.Idempotency
dotnet add package OmniFlow.Observability

# Adapters (choose based on your needs)
dotnet add package OmniFlow.Adapters.RabbitMQ
dotnet add package OmniFlow.Adapters.Sql
dotnet add package OmniFlow.Adapters.MongoDb

# CLI Tools (optional)
dotnet tool install --global OmniFlow.Tools.Cli
```

### Supported Versions

- **.NET:** 8.0 or later
- **C#:** 12.0 or later

---

## 🚀 Quick Start

### 1. Define Your Messages

```csharp
using OmniFlow.Core;

// Commands (imperative)
public record CreateOrder(string OrderId, decimal Amount, string CustomerId) : ICommand;
public record RequestPayment(string OrderId, decimal Amount) : ICommand;

// Events (past tense)
public record OrderCreated(string OrderId, decimal Amount, string CustomerId) : IEvent;
public record PaymentSucceeded(string OrderId, string PaymentId) : IEvent;
public record PaymentFailed(string OrderId, string Reason) : IEvent;
```

### 2. Create Your Saga

```csharp
using OmniFlow.Sagas;

public class OrderSagaState : SagaState
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool PaymentCompleted { get; set; }
}

public class OrderSaga : Saga<OrderSagaState>
{
    protected override async Task OnStartAsync(CancellationToken ct)
    {
        await PublishAsync(new RequestPayment(State.OrderId, State.Amount), ct);
    }

    public async Task HandlePaymentSucceeded(PaymentSucceeded evt, CancellationToken ct)
    {
        State.PaymentCompleted = true;
        await CompleteAsync(ct);
    }

    public async Task HandlePaymentFailed(PaymentFailed evt, CancellationToken ct)
    {
        await CompensateAsync($"Payment failed: {evt.Reason}", ct);
    }

    protected override async Task OnCompensateAsync(CancellationToken ct)
    {
        await PublishAsync(new CancelOrder(State.OrderId), ct);
    }
}
```

### 3. Configure OmniFlow

```csharp
using OmniFlow.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOmniFlow(options =>
{
    options.ServiceName = "OrdersService";

    // Message Bus
    options.MessageBus.Provider = MessageBusProvider.RabbitMQ;
    options.MessageBus.RabbitMQ = new RabbitMQConfig
    {
        HostName = "localhost",
        DeadLetterQueue = new DeadLetterQueueConfig
        {
            Enabled = true,
            MaxRetries = 3
        }
    };

    // Features
    options.EnableSagas = true;
    options.EnableOutbox = true;
    options.EnableIdempotency = true;
    options.EnableObservability = true;

    // Register sagas
    options.RegisterSaga<OrderSaga, OrderSagaState>();
});

var app = builder.Build();
app.Run();
```

---

## ⚙️ Configuration

### Unified Configuration

```csharp
builder.Services.AddOmniFlow(options =>
{
    options.ServiceName = "MyService";
    options.EnableSagas = true;
    options.EnableOutbox = true;
    options.EnableIdempotency = true;
    options.EnableObservability = true;
});
```

### Message Bus Providers

#### InMemory (Development)

```csharp
options.MessageBus.Provider = MessageBusProvider.InMemory;
```

#### RabbitMQ (Production)

```csharp
options.MessageBus.Provider = MessageBusProvider.RabbitMQ;
options.MessageBus.RabbitMQ = new RabbitMQConfig
{
    HostName = "rabbitmq.example.com",
    Port = 5672,
    UserName = "user",
    Password = "password",
    DeadLetterQueue = new DeadLetterQueueConfig
    {
        Enabled = true,
        MaxRetries = 3,
        MessageTtl = TimeSpan.FromDays(7)
    }
};
```

#### Kafka (Coming Soon)

```csharp
options.MessageBus.Provider = MessageBusProvider.Kafka;
options.MessageBus.Kafka = new KafkaConfig
{
    BootstrapServers = "kafka1:9092,kafka2:9092",
    GroupId = "my-group",
    SecurityProtocol = "SASL_SSL",
    SaslMechanism = "SCRAM-SHA-256"
};
```

### Circuit Breaker

```csharp
options.MessageBus.EnableCircuitBreaker = true;
options.MessageBus.CircuitBreakerFailureRatio = 0.5;
options.MessageBus.CircuitBreakerMinimumThroughput = 10;
options.MessageBus.CircuitBreakerSamplingDurationSeconds = 30;
options.MessageBus.CircuitBreakerBreakDurationSeconds = 60;
```

### Persistence

#### SQL Server

```csharp
builder.Services.AddOmniFlowSqlAdapters(
    builder.Configuration.GetConnectionString("OmniFlow")
);
```

#### MongoDB

```csharp
builder.Services.AddOmniFlowMongoDbAdapters<OrderSagaState>(
    connectionString: "mongodb://localhost:27017",
    databaseName: "omniflow"
);
```

### Logging with Serilog

OmniFlow includes integrated Serilog support with automatic correlation ID enrichment.

#### Basic Configuration (Automatic)

```csharp
builder.Services.AddOmniFlow(options =>
{
    options.ServiceName = "OrderService";
    options.EnableObservability = true; // Includes Serilog

    // Logging is automatically configured with correlation IDs
});
```

#### Custom Serilog Configuration

```csharp
builder.Services.AddOmniFlow(options =>
{
    options.ServiceName = "OrderService";

    // Logging configuration
    options.Logging.EnableConsole = true;
    options.Logging.EnableFile = true;
    options.Logging.FilePath = "logs/order-service-.log";
    options.Logging.UseJsonFormat = false;
    options.Logging.MinimumLevel = "Information";
    options.Logging.EnableCorrelationId = true;
    options.Logging.LogLevelOverrides = new Dictionary<string, string>
    {
        ["Microsoft"] = "Warning",
        ["System"] = "Warning",
        ["OmniFlow"] = "Debug"
    };
});
```

#### Manual Serilog with OmniFlow Enrichers

```csharp
using OmniFlow.Observability;

builder.Host.UseSerilog((context, services, configuration) =>
{
    var correlationAccessor = services.GetRequiredService<ICorrelationAccessor>();

    configuration
        .ConfigureOmniFlowSerilog(correlationAccessor, "OrderService")
        .WriteTo.Seq("http://seq-server:5341")
        .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://elastic:9200")));
});
```

#### Configuration via appsettings.json

```json
{
  "OmniFlow": {
    "ServiceName": "OrderService",
    "Logging": {
      "EnableConsole": true,
      "EnableFile": true,
      "FilePath": "logs/order-service-.log",
      "UseJsonFormat": false,
      "MinimumLevel": "Information",
      "EnableCorrelationId": true,
      "LogLevelOverrides": {
        "Microsoft": "Warning",
        "System": "Warning",
        "OmniFlow": "Debug"
      }
    }
  }
}
```

#### Log Output with Automatic Correlation

```
[14:32:15 INF] [OrderService.Handlers.PaymentEventHandler] [abc123-def456] Payment succeeded PAY-789
[14:32:16 WRN] [PaymentService] [abc123-def456] Payment failed: Insufficient funds
[14:32:17 ERR] [OrderService.Sagas.OrderSaga] [abc123-def456] Compensating transaction started
```

Components:
- `14:32:15` - Timestamp
- `INF` - Log level
- `OrderService.Handlers.PaymentEventHandler` - Source context
- `abc123-def456` - **Correlation ID** (automatic!)
- `Payment succeeded PAY-789` - Message

#### Structured Logging

```csharp
// ✅ GOOD - Structured logging
_logger.LogInformation("Order {OrderId} created for customer {CustomerId}", orderId, customerId);

// ❌ BAD - String concatenation
_logger.LogInformation($"Order {orderId} created for customer {customerId}");
```

#### File Logging with Rotation

```csharp
options.Logging.EnableFile = true;
options.Logging.FilePath = "logs/order-service-.log";
```

Output files (automatically rotated daily):
```
logs/order-service-20240115.log
logs/order-service-20240114.log
logs/order-service-20240113.log
```

Set retention limit:
```csharp
// In OmniFlowLoggingOptions
RetainedFileCountLimit = 7; // Keep 7 days
```

#### JSON Format for Log Aggregation

Perfect for ELK, Splunk, or other log aggregation systems:

```csharp
options.Logging.UseJsonFormat = true;
options.Logging.EnableFile = true;
options.Logging.FilePath = "logs/order-service-.json";
```

JSON output:
```json
{
  "Timestamp": "2024-01-15T14:32:15.123Z",
  "Level": "Information",
  "MessageTemplate": "Payment succeeded {PaymentId}",
  "Properties": {
    "PaymentId": "PAY-789",
    "CorrelationId": "abc123-def456",
    "ServiceName": "OrderService",
    "SourceContext": "OrderService.Handlers.PaymentEventHandler"
  }
}
```

#### Logging Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableSerilog` | `bool` | `true` | Enable Serilog configuration |
| `EnableConsole` | `bool` | `true` | Enable console logging |
| `EnableFile` | `bool` | `false` | Enable file logging |
| `FilePath` | `string` | `null` | Log file path with rolling |
| `UseJsonFormat` | `bool` | `false` | Use JSON format for logs |
| `EnableCorrelationId` | `bool` | `true` | Include correlation ID in logs |
| `MinimumLevel` | `string` | `"Information"` | Minimum log level |
| `LogLevelOverrides` | `Dictionary<string, string>` | See above | Override levels by namespace |

### Observability with OpenTelemetry

OmniFlow integrates OpenTelemetry for distributed tracing and metrics.

#### Basic Configuration

```csharp
builder.Services.AddOmniFlow(options =>
{
    options.ServiceName = "OrderService";
    options.EnableObservability = true;

    // Configure tracing
    options.Observability.OtlpEndpoint = "http://jaeger:4317";
    options.Observability.EnablePrometheusExporter = true;
});
```

Logs automatically correlate with traces via correlation ID - view the complete request timeline in Jaeger!

---

## 🎓 Core Concepts

### Message Envelope

```csharp
public sealed class MessageEnvelope<T>
{
    public string MessageId { get; }
    public string CorrelationId { get; }
    public string? CausationId { get; }
    public DateTimeOffset Timestamp { get; }
    public T Message { get; }
    public int SchemaVersion { get; }
}
```

### Saga Lifecycle

```
Start → Running → [Success] → Completed
                ↓ [Failure]
              Compensating → Compensated
```

### Middleware Pipeline

```
Message → Correlation → Logging → Retry → Circuit Breaker → Handler
```

---

## 💡 Usage Examples

### E-Commerce Order Processing

```csharp
public class OrderSaga : Saga<OrderSagaState>
{
    protected override async Task OnStartAsync(CancellationToken ct)
    {
        await PublishAsync(new RequestPayment(State.OrderId, State.Amount), ct);
        await ScheduleTimerAsync(TimeSpan.FromMinutes(10), "PaymentTimeout", ct);
    }

    public async Task HandlePaymentSucceeded(PaymentSucceeded evt, CancellationToken ct)
    {
        await PublishAsync(new ReserveInventory(State.OrderId, State.ProductIds), ct);
    }

    public async Task HandleInventoryReserved(InventoryReserved evt, CancellationToken ct)
    {
        await PublishAsync(new CreateShipment(State.OrderId, State.Address), ct);
    }

    protected override async Task OnCompensateAsync(CancellationToken ct)
    {
        if (State.InventoryReserved)
            await PublishAsync(new ReleaseInventory(State.OrderId), ct);

        if (State.PaymentId != null)
            await PublishAsync(new RefundPayment(State.PaymentId), ct);
    }
}
```

### Idempotent Message Handler

```csharp
await messageBus.SubscribeAsync<OrderCreated>(async (envelope, context) =>
{
    if (!await idempotencyStore.TryRecordAsync(envelope.MessageId, "OrdersService"))
    {
        logger.LogInformation("Duplicate message, skipping");
        return;
    }

    await ProcessOrderAsync(envelope.Message);
});
```

---

## 🔥 Advanced Features

### Saga Timeouts

```csharp
protected override async Task OnStartAsync(CancellationToken ct)
{
    var timerId = await ScheduleTimerAsync(TimeSpan.FromMinutes(30), "OrderTimeout", ct);
}

public async Task HandleTimeout(string timerName, CancellationToken ct)
{
    await CompensateAsync("Order processing timed out", ct);
}
```

### Custom Saga Repository

```csharp
public class RedisSagaRepository<TState> : ISagaRepository<TState>
    where TState : SagaState
{
    public async Task SaveAsync(string sagaId, TState state, int version, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(state);
        await _redis.GetDatabase().StringSetAsync($"saga:{sagaId}", json);
    }
}
```

---

## 🏗️ Architecture

### Project Structure

```
OmniFlow/
├── src/
│   ├── OmniFlow.Core                 # Core primitives
│   ├── OmniFlow.Messaging            # Message bus + middleware
│   ├── OmniFlow.Sagas                # Saga engine
│   ├── OmniFlow.Idempotency          # Idempotency store
│   ├── OmniFlow.Observability        # OpenTelemetry
│   ├── OmniFlow.Adapters.RabbitMQ    # RabbitMQ adapter
│   ├── OmniFlow.Adapters.Sql         # SQL persistence
│   ├── OmniFlow.Adapters.MongoDb     # MongoDB persistence
│   └── OmniFlow.Tools.Cli            # CLI tools
├── samples/
│   ├── OrdersService
│   └── PaymentsService
└── tests/
    └── OmniFlow.Tests
```

### Dependency Graph

```
OmniFlow.Core
  ↓
OmniFlow.Messaging → OmniFlow.Idempotency
  ↓
OmniFlow.Sagas ← OmniFlow.Observability
  ↓
OmniFlow.Adapters.*
```

---

## 🛠️ CLI Tools

### Installation

```bash
dotnet tool install --global OmniFlow.Tools.Cli
```

### Commands

```bash
# List all sagas
omniflow list --connection "Server=localhost;Database=OmniFlow;..."

# Inspect saga details
omniflow inspect --saga-id saga-123 --connection "..."

# Replay failed saga
omniflow replay --saga-id saga-123 --connection "..."
```

**Output Example:**

```
┌──────────────┬───────────┬───────────┬─────────────────────┐
│ Saga ID      │ Type      │ Status    │ Created             │
├──────────────┼───────────┼───────────┼─────────────────────┤
│ saga-123     │ OrderSaga │ Running   │ 2024-01-15 10:30:00 │
│ saga-456     │ OrderSaga │ Completed │ 2024-01-15 10:25:00 │
└──────────────┴───────────┴───────────┴─────────────────────┘
```

---

## 📚 Best Practices

### Message Design

✅ **DO:**
- Use records: `public record OrderCreated(...) : IEvent;`
- Past tense for events: `OrderCreated`, `PaymentSucceeded`
- Imperative for commands: `CreateOrder`, `RequestPayment`
- Include all necessary data

❌ **DON'T:**
- Don't include behavior in messages
- Don't use mutable classes
- Don't create circular dependencies

### Saga Design

✅ **DO:**
- Use `PublishAsync()` to publish messages
- Check `State.Status` before processing
- Register sagas as transient
- Use timeouts for long-running sagas

❌ **DON'T:**
- Don't mutate state without save methods
- Don't share saga instances
- Don't call `MessageBus.PublishAsync()` directly
- Don't swallow exceptions

### Error Handling

✅ **DO:**
- Let exceptions bubble up
- Use compensation for business failures
- Log with correlation IDs
- Set up DLQ for poison messages

❌ **DON'T:**
- Don't swallow exceptions
- Don't retry indefinitely
- Don't ignore idempotency

---

## 🐛 Troubleshooting

### Saga not persisting state

**Cause:** Forgot to call save methods

**Solution:**
```csharp
// ❌ Wrong
await MessageBus.PublishAsync(message);

// ✅ Correct
await PublishAsync(message, ct);
```

### Duplicate messages processed

**Cause:** Missing idempotency check

**Solution:**
```csharp
if (!await idempotencyStore.TryRecordAsync(envelope.MessageId, "MyService"))
    return;
```

### Circuit breaker not opening

**Cause:** Throughput below minimum

**Solution:**
```csharp
options.MessageBus.CircuitBreakerMinimumThroughput = 5;
```

### Enable Debug Logging

```json
{
  "Logging": {
    "LogLevel": {
      "OmniFlow": "Debug"
    }
  }
}
```

---

## 🌍 Environment Configuration

### Development

```csharp
if (builder.Environment.IsDevelopment())
{
    options.MessageBus.Provider = MessageBusProvider.InMemory;
    options.EnableOutbox = false;
}
```

### Production

```csharp
else
{
    options.MessageBus.Provider = MessageBusProvider.RabbitMQ;
    builder.Services.AddOmniFlowSqlAdapters(connectionString);
    options.EnableOutbox = true;
}
```

---

## 🤝 Contributing

We welcome contributions!

### Development Setup

```bash
git clone https://github.com/Seshathri77/Omni.git
cd Omni
dotnet restore
dotnet build
dotnet test
```

### Docker Compose

```bash
docker-compose up -d

# Services:
# - RabbitMQ: http://localhost:15672
# - Jaeger: http://localhost:16686
# - Seq: http://localhost:5341
# - Prometheus: http://localhost:9090
```

---

## 📄 License

MIT License - see [LICENSE](LICENSE) file.

---

## 🙏 Acknowledgments

- **Polly** - Resilience patterns
- **OpenTelemetry** - Distributed tracing
- **Serilog** - Structured logging
- **RabbitMQ** - Message broker
- **Entity Framework Core** - Data access

---

## 📞 Support

- **Issues:** [GitHub Issues](https://github.com/Seshathri77/Omni/issues)
- **Discussions:** [GitHub Discussions](https://github.com/Seshathri77/Omni/discussions)

---

## 🗺️ Roadmap

- [ ] Kafka adapter
- [ ] Azure Service Bus adapter
- [ ] Saga visualization UI
- [ ] Schema evolution
- [ ] Helm charts for Kubernetes

---

**Made with ❤️ by the OmniFlow Team**
