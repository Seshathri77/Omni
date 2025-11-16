# OmniFlow.Framework - Complete Documentation

**Version:** 1.0  
**Last Updated:** November 16, 2025

A comprehensive .NET 8 framework for building resilient, observable microservices with built-in support for saga orchestration, distributed tracing, and idempotent message processing.

---

## Table of Contents

1. [Overview](#overview)
2. [Features](#features)
3. [Project Structure](#project-structure)
4. [Quick Start](#quick-start)
5. [Key Concepts](#key-concepts)
6. [Testing with RabbitMQ](#testing-with-rabbitmq)
7. [Observability](#observability)
8. [Prometheus Metrics](#prometheus-metrics)
9. [Production Deployment](#production-deployment)
10. [Troubleshooting](#troubleshooting)

---

## Overview

OmniFlow is a production-ready microservices framework that provides:
- **Saga orchestration** for managing distributed transactions
- **Distributed tracing** with OpenTelemetry and Jaeger
- **Structured logging** with Serilog and Seq
- **Metrics collection** with Prometheus
- **Message-driven architecture** with RabbitMQ support
- **Built-in idempotency** for exactly-once processing

---

## Features

### Core Capabilities

- **Correlation & Distributed Tracing**: Automatic correlation ID propagation across service boundaries with OpenTelemetry integration
- **Saga Orchestration**: Durable saga engine with compensation logic for managing distributed transactions
- **Pluggable Message Bus**: Abstract message bus with in-memory and RabbitMQ adapters
- **Idempotency**: Built-in idempotency middleware to prevent duplicate message processing
- **Observability**: OpenTelemetry traces, metrics, and Serilog-based structured logging
- **Resilience**: Polly-based retry policies and circuit breakers
- **Schema Evolution**: Message versioning support for schema migration
- **Message Signing**: HMAC-based message authentication and validation

---

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

---

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
builder.Services.AddOmniFlowObservability("MyService", 
    enablePrometheusExporter: true);

// Register your sagas
builder.Services.AddSaga<OrderSaga, OrderSagaState>();

var app = builder.Build();

// Enable Prometheus metrics
app.UsePrometheusScrapingEndpoint();

app.Run();
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

---

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

---

## Testing with RabbitMQ

### Prerequisites

#### Install RabbitMQ

**Option A: Docker (Recommended)**
```powershell
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

**Option B: Windows Installer**
- Download from https://www.rabbitmq.com/download.html
- Install Erlang first, then RabbitMQ
- Enable management plugin: `rabbitmq-plugins enable rabbitmq_management`

#### Verify RabbitMQ is Running

- RabbitMQ Management UI: http://localhost:15672
- Default credentials: `guest` / `guest`

### Configuration

Both services are configured to connect to RabbitMQ via `appsettings.json`:

```json
{
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  }
}
```

### Running the Services

#### Terminal 1: Start OrdersService
```powershell
cd samples\OrdersService
dotnet run
```
- Runs on: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger`

#### Terminal 2: Start PaymentsService
```powershell
cd samples\PaymentsService
dotnet run
```
- Runs on a different port (check console output)

**Important**: Both services must run simultaneously since PaymentsService responds to OrdersService saga events.

### Testing the Saga Flow

#### 1. Create an Order (Start OrderSaga)

```powershell
curl -X POST http://localhost:5000/api/orders `
  -H "Content-Type: application/json" `
  -d '{"amount": 99.99, "customerId": "cust-123"}'
```

**Response:**
```json
{
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Accepted"
}
```

#### 2. Check Order Status

```powershell
curl http://localhost:5000/api/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Successful Payment Response:**
```json
{
  "sagaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "cust-123",
  "amount": 99.99,
  "paymentCompleted": true,
  "status": "Completed",
  "history": [
    "Started OrderSaga",
    "Requesting payment...",
    "Payment completed successfully"
  ]
}
```

**Failed Payment Response (20% chance):**
```json
{
  "sagaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "cust-123",
  "amount": 99.99,
  "paymentCompleted": false,
  "status": "Compensated",
  "history": [
    "Started OrderSaga",
    "Requesting payment...",
    "Payment failed",
    "Compensating: Cancelling order..."
  ]
}
```

### Understanding the Message Flow

#### Successful Payment Saga
1. **OrdersService** receives POST /api/orders
2. **OrderSaga** publishes `OrderCreated` event
3. **OrderSaga** starts and publishes `PaymentRequested` command
4. **RabbitMQ** routes message to PaymentsService queue
5. **PaymentsService** processes payment (80% success rate)
6. **PaymentsService** publishes `PaymentSucceeded` event
7. **RabbitMQ** routes message back to OrdersService queue
8. **OrderSaga** handles `PaymentSucceeded` and completes

#### Failed Payment Saga (Compensation)
1. Steps 1-5 same as above
2. **PaymentsService** publishes `PaymentFailed` event (20% chance)
3. **RabbitMQ** routes message back to OrdersService
4. **OrderSaga** handles `PaymentFailed` and triggers compensation
5. **OrderSaga.OnCompensateAsync()** publishes `OrderCancelled` event
6. **OrderSaga** status changes to `Compensated`

### Monitoring with RabbitMQ Management

Open http://localhost:15672 and check:

#### Exchanges
- **omniflow.exchange** (topic exchange)
  - Routes all OmniFlow messages

#### Queues
- **OrdersService** queue
  - Binds to: `OrdersService.Messages.OrderCreated`, `OrdersService.Messages.PaymentSucceeded`, `OrdersService.Messages.PaymentFailed`
  - Check message rates and queue depth

- **PaymentsService** queue
  - Binds to: `OrdersService.Messages.PaymentRequested`
  - Check processing rates

#### Message Flow
- Watch messages being published and consumed in real-time
- Check message details (headers, correlation IDs, payloads)

### Testing Scenarios

#### 1. High Volume Test (100 orders)
```powershell
1..100 | ForEach-Object {
    curl -X POST http://localhost:5000/api/orders `
      -H "Content-Type: application/json" `
      -d "{`"amount`": $($_), `"customerId`": `"cust-$($_)`"}"
}
```
Expected: ~80 completed, ~20 compensated

#### 2. Service Failure Resilience
1. Start OrdersService
2. Create order (POST /api/orders) - saga starts, publishes PaymentRequested
3. **Kill PaymentsService** before payment processes
4. Check RabbitMQ - message queued in PaymentsService queue
5. **Restart PaymentsService** - message processed, saga completes
6. Verify order status updated correctly

#### 3. Idempotency Test
1. Create order with specific correlation ID
2. Manually reprocess same message (simulate duplicate)
3. Verify saga state not duplicated
4. Check logs for "Message already processed" (if idempotency middleware enabled)

---

## Observability

### Quick Start - Docker Compose

Start all observability tools at once:

```powershell
docker-compose -f docker-compose-observability.yml up -d
```

This starts:
- **RabbitMQ** (port 15672) - Message broker
- **Jaeger** (port 16686) - Distributed tracing
- **Seq** (port 5341) - Structured logging
- **Prometheus** (port 9090) - Metrics
- **Grafana** (port 3000) - Dashboards

### 1. Distributed Tracing with Jaeger

**Access:** http://localhost:16686

#### What You'll See

**Service Map:**
- Visual representation of services communicating
- OrdersService → RabbitMQ → PaymentsService flow
- Request duration and error rates

**Trace Timeline:**
- Complete saga execution from start to finish
- Shows message publish → queue → consume latencies
- Correlation IDs link related operations

#### How to Use

1. **Select Service:** Choose "OrdersService" from dropdown
2. **Find Traces:** Click "Find Traces" button
3. **View Details:** Click on a trace to see:
   - Span timeline (message processing steps)
   - Tags: `saga.id`, `messaging.correlation_id`, `messaging.message_type`
   - Logs: State transitions, compensation events
4. **Compare Traces:** Compare successful vs failed payment sagas

#### Key Traces to Look For

- `POST /api/orders` → Full order saga execution
- `MessageBus.Publish OrderCreated` → Message flow
- `Saga.Start` → Saga initialization
- `Saga.Compensate` → Rollback when payment fails

### 2. Structured Logging with Seq

**Access:** http://localhost:5341  
**Credentials:** admin / Admin123!

#### What You'll See

**Structured Logs:**
- All log entries from both services
- Filterable by properties (CorrelationId, OrderId, SagaId)
- Real-time log streaming

#### How to Use

1. **Search by Correlation:** 
   ```
   CorrelationId = 'abc-123'
   ```
   See all logs for a single order

2. **Filter by Level:**
   ```
   @Level = 'Error'
   ```
   Find failures only

3. **Query Properties:**
   ```
   OrderId is not null
   ```
   All order-related logs

4. **Build Dashboards:**
   - Create saved queries
   - Pin important metrics
   - Set up alerts

#### Useful Queries

```csharp
// All saga operations
@MessageTemplate like '%Saga%'

// Payment failures
@MessageTemplate like '%Payment failed%'

// Duplicate messages (idempotency)
@MessageTemplate like '%Duplicate message%'

// By service
Application = 'OrdersService'
```

### 3. Console Output

Current implementation logs to console with correlation IDs:

```
[17:30:45 INF] [CorrelationId: abc-123] Publishing message OrderCreated
[17:30:45 INF] [CorrelationId: abc-123] Saga transition: Running
[17:30:46 INF] [CorrelationId: abc-123] Processing payment for order abc-123, amount 99.99
[17:30:47 INF] [CorrelationId: abc-123] Payment succeeded for order abc-123
[17:30:47 INF] [CorrelationId: abc-123] Saga transition: Completed
```

### Correlation ID Tracing

Every operation includes a correlation ID that flows through:
1. HTTP request → Controller
2. Message publication → RabbitMQ
3. Message consumption → Saga handler
4. Response back to client

**To trace a request:**
```powershell
# 1. Create order, note the OrderId
$orderId = (curl -X POST http://localhost:5000/api/orders -d '{"amount": 50, "customerId": "test"}' | ConvertFrom-Json).orderId

# 2. Search in Seq
# CorrelationId = '$orderId'

# 3. Search in Jaeger
# Tags: correlation_id = $orderId
```

---

## Prometheus Metrics

### Overview

OmniFlow.Observability includes **built-in Prometheus support** via OpenTelemetry. Metrics are automatically exposed at `/metrics`.

### Setup

Prometheus metrics are **automatically enabled** when you configure:

```csharp
builder.Services.AddOmniFlowObservability(
    "ServiceName",
    tracing => { /* ... */ },
    enablePrometheusExporter: true); // 👈 Enables /metrics endpoint

// After app.Build()
app.UsePrometheusScrapingEndpoint(); // Maps /metrics endpoint
```

### Verify Prometheus

- **Check Targets:** http://localhost:9090/targets
  - Should show `host.docker.internal:5000` (OrdersService) - **UP**
  - Should show `host.docker.internal:5001` (PaymentsService) - **UP**

- **View Raw Metrics:**
  - http://localhost:5000/metrics
  - http://localhost:5001/metrics

### Available Metrics

#### ASP.NET Core Metrics (Automatic)

| Metric | Description | Type |
|--------|-------------|------|
| `http_server_request_duration_seconds` | HTTP request latency | Histogram |
| `http_server_active_requests` | Current active requests | Gauge |
| `http_server_request_body_size_bytes` | Request body sizes | Histogram |
| `http_server_response_body_size_bytes` | Response body sizes | Histogram |

**Labels:** `http_request_method`, `http_response_status_code`, `http_route`, `service_name`

#### .NET Runtime Metrics (Automatic)

| Metric | Description | Type |
|--------|-------------|------|
| `process_runtime_dotnet_gc_collections_count` | GC collections by generation | Counter |
| `process_runtime_dotnet_gc_heap_size_bytes` | Managed heap size | Gauge |
| `process_runtime_dotnet_gc_allocated_bytes` | Total allocated memory | Counter |
| `process_runtime_dotnet_thread_pool_threads_count` | Thread pool threads | Gauge |
| `process_runtime_dotnet_exceptions_count` | Exception count | Counter |

#### OmniFlow Custom Metrics (Manual Recording)

| Metric | Description | Type |
|--------|-------------|------|
| `omniflow_messages_published` | Published messages | Counter |
| `omniflow_messages_processed` | Processed messages | Counter |
| `omniflow_messages_failed` | Failed messages | Counter |
| `omniflow_messages_processing_duration` | Processing time (ms) | Histogram |
| `omniflow_sagas_started` | Started sagas | Counter |
| `omniflow_sagas_completed` | Completed sagas | Counter |
| `omniflow_sagas_compensated` | Compensated sagas | Counter |

**To record custom metrics:**
```csharp
var metrics = app.Services.GetRequiredService<OmniFlowMetrics>();
metrics.RecordMessagePublished("OrderCreated");
metrics.RecordSagaStarted("OrderSaga");
metrics.RecordProcessingDuration("PaymentRequested", 123.45);
```

### Essential PromQL Queries

#### Service Health

```promql
# Request rate by service (req/sec)
rate(http_server_request_duration_seconds_count{service_name="OrdersService"}[1m])

# P95 latency (seconds)
histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m]))

# Error rate (4xx + 5xx responses)
sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"4..|5.."}[1m]))

# Active concurrent requests
sum by (service_name) (http_server_active_requests)
```

#### Performance Monitoring

```promql
# Memory pressure (heap size in MB)
process_runtime_dotnet_gc_heap_size_bytes{service_name="OrdersService"} / 1024 / 1024

# GC frequency (collections/sec)
rate(process_runtime_dotnet_gc_collections_count{generation="gen2"}[1m])

# Thread pool saturation
process_runtime_dotnet_thread_pool_threads_count{state="busy"} / 
  on(service_name) group_left() 
  sum by (service_name) (process_runtime_dotnet_thread_pool_threads_count)
```

#### Business Metrics

```promql
# Saga success rate (percentage)
100 * (
  rate(omniflow_sagas_completed[5m]) / 
  rate(omniflow_sagas_started[5m])
)

# Message throughput by type
sum by (message_type) (rate(omniflow_messages_published[1m]))

# Failed message rate
rate(omniflow_messages_failed[1m])
```

#### Alerting Queries

```promql
# High error rate (>5%)
(
  sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m])) /
  sum(rate(http_server_request_duration_seconds_count[5m]))
) > 0.05

# Slow requests (P95 > 1s)
histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m])) > 1

# Memory leak (heap growing continuously)
deriv(process_runtime_dotnet_gc_heap_size_bytes[10m]) > 1048576

# Saga failure spike
rate(omniflow_sagas_compensated[5m]) > 10
```

### Grafana Dashboards

#### Add Prometheus Data Source

1. Go to http://localhost:3000 (admin/admin)
2. Configuration → Data Sources → Add Prometheus
3. URL: `http://prometheus:9090`
4. Click "Save & Test"

#### Recommended Community Dashboards

Import these (Dashboard → Import → Enter ID):
- **3662** - ASP.NET Core metrics
- **12633** - .NET Runtime metrics
- **7587** - HTTP server metrics

#### Custom OmniFlow Dashboard

Create panels with these queries:

**Panel 1: Order Processing Rate**
```promql
sum(rate(omniflow_sagas_started{saga_type="OrderSaga"}[1m]))
```

**Panel 2: Payment Success Rate**
```promql
100 * (
  sum(rate(omniflow_sagas_completed[5m])) / 
  sum(rate(omniflow_sagas_started[5m]))
)
```

**Panel 3: Message Processing Latency (P95)**
```promql
histogram_quantile(0.95, 
  rate(omniflow_messages_processing_duration_bucket[5m])
)
```

---

## Production Deployment

### Configuration

#### Replace In-Memory Components

```csharp
// In appsettings.Production.json
{
  "RabbitMQ": {
    "HostName": "rabbitmq-cluster.company.com",
    "Port": 5671,
    "UserName": "omniflow-user",
    "Password": "${RABBITMQ_PASSWORD}",  // Use environment variable
    "VirtualHost": "/production",
    "UseSsl": true,
    "RequestedHeartbeat": 60
  },
  "ConnectionStrings": {
    "OmniFlow": "Server=sql.company.com;Database=OmniFlow;..."
  },
  "Jaeger": {
    "Endpoint": "http://jaeger-collector:4317"
  },
  "Seq": {
    "ServerUrl": "https://seq.production.company.com",
    "ApiKey": "${SEQ_API_KEY}"
  }
}

// In Program.cs
// Use SQL adapters
builder.Services.AddOmniFlowSqlAdapters(
    builder.Configuration.GetConnectionString("OmniFlow")!);

// Use RabbitMQ
builder.Services.AddRabbitMQMessageBus(options =>
{
    builder.Configuration.GetSection("RabbitMQ").Bind(options);
});

// Configure observability
builder.Services.AddOmniFlowObservability("OrdersService", 
    tracing =>
    {
        tracing.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(builder.Configuration["Jaeger:Endpoint"]!);
        });
        tracing.SetSampler(new TraceIdRatioBasedSampler(0.1)); // Sample 10%
    },
    enablePrometheusExporter: true);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Seq(
        builder.Configuration["Seq:ServerUrl"]!,
        apiKey: builder.Configuration["Seq:ApiKey"])
    .CreateLogger();
```

### Prometheus Production Configuration

```yaml
# prometheus.yml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'omniflow-services'
    static_configs:
      - targets:
          - 'orders-service.prod.svc:5000'
          - 'payments-service.prod.svc:5000'
    relabel_configs:
      - source_labels: [__address__]
        target_label: instance

# Retention
command:
  - '--storage.tsdb.retention.time=30d'
  - '--storage.tsdb.retention.size=10GB'
```

### High Availability

- **RabbitMQ**: Use clustered setup with mirrored queues
- **SQL**: Use Always On Availability Groups or replicas
- **Prometheus**: Use Thanos or Cortex for long-term storage
- **Jaeger**: Use Elasticsearch or Cassandra backend

---

## Troubleshooting

### Services can't connect to RabbitMQ
- Verify RabbitMQ is running: `docker ps` or check Windows Services
- Check connection settings in `appsettings.json`
- Ensure port 5672 is not blocked by firewall

### Messages not being consumed
- Check queue bindings in RabbitMQ Management UI
- Verify exchange is type "topic" (not "direct" or "fanout")
- Check routing keys match message type names

### Saga stuck in "Running" status
- Check PaymentsService logs for errors
- Verify both services are running
- Check RabbitMQ queues for undelivered messages

### Prometheus targets show "DOWN"
```powershell
# Check services are running
curl http://localhost:5000/metrics
curl http://localhost:5001/metrics

# Check Docker connectivity
docker exec omniflow-prometheus wget -O- http://host.docker.internal:5000/metrics

# Check Prometheus logs
docker logs omniflow-prometheus
```

### Services not showing in Jaeger
- Check OTLP endpoint is accessible: `curl http://localhost:4317`
- Verify firewall rules
- Check service logs for exporter errors

### Logs not appearing in Seq
- Test Seq ingestion: `curl http://localhost:5341/api`
- Check Seq API key is correct
- Verify Serilog sink configuration

### Missing correlation IDs
- Ensure `CorrelationMiddleware` is registered
- Check `ICorrelationAccessor` is injected
- Verify `CorrelationIdEnricher` is added to Serilog

### Build errors
```powershell
dotnet clean
dotnet restore
dotnet build
```

---

## Summary

### Observability Stack

| Tool | Purpose | Access | Key Feature |
|------|---------|--------|-------------|
| **Console** | Basic logging | Terminal | Quick debugging |
| **Seq** | Structured logs | :5341 | Powerful queries, alerts |
| **Jaeger** | Distributed tracing | :16686 | Service map, span timeline |
| **Prometheus** | Metrics | :9090 | Time-series data, PromQL |
| **Grafana** | Dashboards | :3000 | Visual insights, alerting |
| **RabbitMQ** | Message broker | :15672 | Queue monitoring |

### Development Workflow

1. **Start observability stack:**
   ```powershell
   docker-compose -f docker-compose-observability.yml up -d
   ```

2. **Start services:**
   ```powershell
   # Terminal 1
   cd samples\OrdersService
   dotnet run
   
   # Terminal 2
   cd samples\PaymentsService
   dotnet run
   ```

3. **Test the system:**
   ```powershell
   curl -X POST http://localhost:5000/api/orders `
     -H "Content-Type: application/json" `
     -d '{"amount": 99.99, "customerId": "cust-123"}'
   ```

4. **Monitor:**
   - Logs: http://localhost:5341
   - Traces: http://localhost:16686
   - Metrics: http://localhost:9090
   - Messages: http://localhost:15672

### Architecture

```
┌─────────────────┐         RabbitMQ          ┌──────────────────┐
│ OrdersService   │ ──PaymentRequested──────> │ PaymentsService  │
│   Port 5000     │                            │   Port 5001      │
│                 │ <──PaymentSucceeded/Failed─┤                  │
└─────────────────┘                            └──────────────────┘
        │                                               │
        │ OrderSaga orchestrates                        │ Processes payments
        │ long-running workflow                         │ and publishes events
        ↓                                               ↓
   Completed/Compensated                          Success/Failure
        │                                               │
        └───────────────── Observability ──────────────┘
                  │              │              │
              Jaeger (Traces) Seq (Logs) Prometheus (Metrics)
```

---

## Resources

- [Saga Pattern](https://microservices.io/patterns/data/saga.html)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
- [Prometheus Query Language](https://prometheus.io/docs/prometheus/latest/querying/basics/)
- [RabbitMQ Documentation](https://www.rabbitmq.com/documentation.html)
- [Seq Query Language](https://docs.datalust.co/docs/the-seq-query-language)

---

## License

MIT

---

**End of Documentation**
