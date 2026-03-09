# .NET Aspire Integration for OmniFlow eCommerce Sample

## Overview

The eCommerce sample now includes .NET Aspire support for simplified local development and orchestration. Aspire provides:

- **Orchestration Dashboard** - Visual management of all services and infrastructure
- **Service Discovery** - Automatic service-to-service communication
- **Observability** - Built-in OpenTelemetry integration
- **Resource Management** - Easy configuration of RabbitMQ, Jaeger, Prometheus, Grafana
- **Health Checks** - Automatic health monitoring for all services

## What is .NET Aspire?

.NET Aspire is an opinionated, cloud-ready stack for building observable, production-ready, distributed applications. It includes:

- **App Host** - Orchestrates your application and resources
- **Service Defaults** - Shared configuration for observability and resilience
- **Dashboard** - Real-time view of your application
- **Components** - Pre-configured integrations for popular services

## Project Structure

```
samples/
├── ECommerce.AppHost/              ✨ NEW - Aspire orchestration
│   ├── Program.cs                  # App configuration
│   └── appsettings.json
│
├── ECommerce.ServiceDefaults/      ✨ NEW - Shared configuration
│   └── Extensions.cs               # OpenTelemetry, health checks
│
├── ECommerce.OrderService/         ✅ Updated for Aspire
│   └── Program.cs                  # Uses ServiceDefaults
│
└── ECommerce.PaymentService/       ✅ Updated for Aspire
    └── Program.cs                  # Uses ServiceDefaults
```

## Running with Aspire

### Quick Start

```bash
cd samples/ECommerce.AppHost
dotnet run
```

The Aspire dashboard will open automatically at: **https://localhost:17000**

### What You Get

**Aspire Dashboard** shows:
- 📊 **Resources** - All services and infrastructure
- 📈 **Metrics** - Real-time performance data
- 🔍 **Traces** - Distributed request tracing
- 📝 **Logs** - Centralized structured logs
- 🌐 **Endpoints** - All service URLs

**Automatically Started:**
- ✅ Order Service (http://localhost:5001)
- ✅ Payment Service (http://localhost:5002)
- ✅ RabbitMQ (http://localhost:15672) - guest/guest
- ✅ Jaeger UI (http://localhost:16686)
- ✅ Prometheus (http://localhost:9090)
- ✅ Grafana (http://localhost:3000) - admin/admin

### Dashboard Features

#### Resources Tab
```
┌─────────────────┬─────────┬──────────────────┬──────────┐
│ Name            │ Type    │ State            │ Endpoint │
├─────────────────┼─────────┼──────────────────┼──────────┤
│ orderservice    │ Project │ Running          │ :5001    │
│ paymentservice  │ Project │ Running          │ :5002    │
│ rabbitmq        │ Container Running          │ :15672   │
│ jaeger          │ Container Running          │ :16686   │
│ prometheus      │ Container Running          │ :9090    │
│ grafana         │ Container Running          │ :3000    │
└─────────────────┴─────────┴──────────────────┴──────────┘
```

#### Traces Tab
- View end-to-end request traces
- See correlation between Order and Payment services
- Inspect message flow through RabbitMQ

#### Logs Tab
- Unified logs from all services
- Filter by service, log level, or correlation ID
- Real-time log streaming

#### Metrics Tab
- HTTP request rates and durations
- Message bus metrics
- Custom OmniFlow metrics

## Configuration

### AppHost (Program.cs)

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithDataVolume();

var jaeger = builder.AddContainer("jaeger", "jaegertracing/all-in-one")
    .WithHttpEndpoint(port: 16686, targetPort: 16686, name: "ui")
    .WithEndpoint(port: 4317, targetPort: 4317, name: "otlp-grpc");

// Services
var orderService = builder.AddProject<Projects.ECommerce_OrderService>("orderservice")
    .WithReference(rabbitmq)
    .WithReference(jaeger)
    .WithEnvironment("OmniFlow__MessageBus__Provider", "RabbitMQ");

builder.Build().Run();
```

### Service Defaults

All services automatically get:

**OpenTelemetry:**
- Automatic trace collection
- Metric collection
- Log forwarding to dashboard

**Resilience:**
- HTTP client retry policies
- Circuit breakers
- Timeout handling

**Health Checks:**
- `/health` - Overall health
- `/alive` - Liveness probe

## Benefits Over Docker Compose

| Feature | Docker Compose | .NET Aspire |
|---------|---------------|-------------|
| **Dashboard** | ❌ No | ✅ Built-in |
| **Service Discovery** | ⚠️ Manual | ✅ Automatic |
| **Observability** | ⚠️ Separate tools | ✅ Integrated |
| **Configuration** | YAML | ✅ C# with IntelliSense |
| **Hot Reload** | ❌ No | ✅ Yes |
| **Debugging** | ⚠️ Complex | ✅ Simple |
| **Local Dev** | ✅ Good | ✅ Excellent |
| **Production** | ✅ Yes | ⚠️ Local/Dev focused |

## Use Cases

### When to Use Aspire

✅ **Local Development** - Best experience for developers  
✅ **Testing** - Quick iteration with hot reload  
✅ **Demos** - Easy to showcase the system  
✅ **Learning** - Clear visualization of architecture  

### When to Use Docker Compose

✅ **CI/CD** - Standard Docker deployment  
✅ **Production** - Proven container orchestration  
✅ **Multi-platform** - Works everywhere Docker runs  
✅ **Existing Infrastructure** - Already using Docker  

### When to Use Both

✅ **Best Practice**: Use Aspire for dev, Docker Compose for CI/production!

## Testing the System with Aspire

### 1. Start Aspire

```bash
cd samples/ECommerce.AppHost
dotnet run
```

### 2. Create Test Order

Use the Aspire dashboard or direct API:

```bash
curl -X POST http://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "CUST-001",
    "items": [{
      "productId": "LAPTOP-001",
      "productName": "Dell XPS 15",
      "quantity": 1,
      "unitPrice": 1299.99
    }],
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

### 3. Observe in Dashboard

**Traces Tab:**
- See the complete order flow
- OrderService → RabbitMQ → PaymentService
- Timing for each step

**Logs Tab:**
- Filter by correlation ID
- See logs from both services
- Automatic correlation

**Metrics Tab:**
- HTTP request count
- Message processing rate
- Error rates

## Advanced Configuration

### Custom Resource Configuration

```csharp
// Add PostgreSQL for saga persistence
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("omniflow");

var orderService = builder.AddProject<Projects.ECommerce_OrderService>("orderservice")
    .WithReference(postgres)
    .WithEnvironment("ConnectionStrings__OmniFlow", postgres.ConnectionStringExpression);
```

### Environment-Specific Settings

```csharp
// AppHost Program.cs
var jaegerEndpoint = builder.Configuration["JAEGER_ENDPOINT"] ?? "http://localhost:4317";

var orderService = builder.AddProject<Projects.ECommerce_OrderService>("orderservice")
    .WithEnvironment("OmniFlow__Observability__OtlpEndpoint", jaegerEndpoint);
```

### Adding More Services

```csharp
// Add Inventory Service
var inventoryService = builder.AddProject<Projects.ECommerce_InventoryService>("inventoryservice")
    .WithReference(rabbitmq)
    .WithReference(jaeger);

// Add Notification Service
var notificationService = builder.AddProject<Projects.ECommerce_NotificationService>("notificationservice")
    .WithReference(rabbitmq);
```

## Debugging with Aspire

### Attach Debugger

1. Start Aspire: `dotnet run`
2. In Visual Studio/Rider: **Attach to Process**
3. Select `ECommerce.OrderService` or `ECommerce.PaymentService`
4. Set breakpoints and debug!

### Hot Reload

Changes to service code automatically reload:
- Edit `OrderSaga.cs`
- Save file
- Service restarts with changes
- No need to restart AppHost!

## Health Checks

Services expose health endpoints:

```bash
# Overall health
curl http://localhost:5001/health

# Liveness probe
curl http://localhost:5001/alive
```

Aspire dashboard shows health status in real-time.

## Troubleshooting

### Services Not Starting

**Problem**: Services show "Failed" status

**Solution**: Check logs in dashboard Logs tab
```bash
# Or check directly
dotnet run --project ECommerce.OrderService
```

### RabbitMQ Connection Issues

**Problem**: Services can't connect to RabbitMQ

**Solution**: Aspire uses service discovery - ensure services reference RabbitMQ:
```csharp
.WithReference(rabbitmq)
```

### Dashboard Not Opening

**Problem**: Dashboard doesn't open at https://localhost:17000

**Solution**: Check port availability
```bash
netstat -ano | findstr :17000
```

Or change port in `launchSettings.json`:
```json
{
  "applicationUrl": "https://localhost:18000"
}
```

## Migration from Docker Compose

### Before (Docker Compose)

```bash
docker-compose up
# Terminal 1
cd ECommerce.OrderService && dotnet run

# Terminal 2
cd ECommerce.PaymentService && dotnet run
```

### After (Aspire)

```bash
cd ECommerce.AppHost
dotnet run
```

Everything starts automatically!

## Production Deployment

**Note**: Aspire is optimized for local development. For production:

1. **Use AppHost for manifests:**
```bash
dotnet run --project ECommerce.AppHost -- --publisher manifest
```

2. **Generate deployment artifacts:**
- Kubernetes manifests
- Docker Compose files
- Azure Container Apps

3. **Or use Docker Compose** as before for production.

## Learn More

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard)
- [Service Discovery](https://learn.microsoft.com/en-us/dotnet/aspire/service-discovery/overview)
- [OmniFlow README](../../README.md)

## Summary

.NET Aspire provides:

✅ **Unified Dashboard** - See everything in one place  
✅ **Service Discovery** - Automatic inter-service communication  
✅ **Hot Reload** - Fast development iteration  
✅ **Integrated Observability** - Traces, logs, metrics  
✅ **Easy Setup** - One command to start everything  
✅ **Great DX** - Best developer experience  

**Perfect for OmniFlow local development and demos!**

---

**Choose your workflow:**
- 🚀 **Development**: Use Aspire (`dotnet run`)
- 🐳 **CI/Production**: Use Docker Compose (`docker-compose up`)
- ✨ **Best**: Use both!
