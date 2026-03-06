# OmniFlow.Adapters.AzureServiceBus

Azure Service Bus adapter for OmniFlow message bus, providing enterprise-grade cloud messaging with advanced features.

## Features

- ✅ **Cloud-Native**: Fully managed Azure service with high availability
- ✅ **Session Support**: Ordered processing using correlation ID as session
- ✅ **Dead-Letter Queue**: Automatic handling of failed messages
- ✅ **At-Least-Once Delivery**: PeekLock mode with manual completion
- ✅ **Auto-Scaling**: Dynamic scaling based on queue depth
- ✅ **Managed Identity**: Passwordless authentication with Azure AD
- ✅ **Message Filtering**: Subscription rules based on message properties
- ✅ **Duplicate Detection**: Built-in deduplication using MessageId

## Installation

```bash
dotnet add package OmniFlow.Adapters.AzureServiceBus
```

## Quick Start

### Using Connection String

```csharp
using OmniFlow.Adapters.AzureServiceBus;
using OmniFlow.Core;

var builder = WebApplication.CreateBuilder(args);

// Register OmniFlow core services
builder.Services.AddOmniFlowCore();

// Use Azure Service Bus
builder.Services.AddAzureServiceBusMessageBus(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("ServiceBus");
    options.TopicName = "omniflow";
    options.ServiceName = "orders-service";
    options.EnableSessions = true; // For ordered processing
});

var app = builder.Build();
```

### Using Managed Identity (Recommended for Production)

```csharp
builder.Services.AddAzureServiceBusMessageBus(options =>
{
    options.FullyQualifiedNamespace = "myservicebus.servicebus.windows.net";
    options.TopicName = "omniflow";
    options.ServiceName = "orders-service";
    options.EnableSessions = true;
});
```

### Publishing Messages

```csharp
public class OrdersController : ControllerBase
{
    private readonly IMessageBus _messageBus;

    public OrdersController(IMessageBus messageBus)
    {
        _messageBus = messageBus;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        var orderCreated = new OrderCreated(
            OrderId: Guid.NewGuid().ToString(),
            Amount: request.Amount,
            CustomerId: request.CustomerId
        );

        await _messageBus.PublishAsync(orderCreated);
        return Accepted(new { orderCreated.OrderId });
    }
}
```

### Subscribing to Messages

```csharp
var app = builder.Build();

var messageBus = app.Services.GetRequiredService<IMessageBus>();

// Subscribe to OrderCreated messages
await messageBus.SubscribeAsync<OrderCreated>(async (envelope, context) =>
{
    var orderCreated = envelope.Message;
    
    // Process the order...
    Console.WriteLine($"Received order: {orderCreated.OrderId}");
    
    // Message is automatically completed after successful processing
    // or moved to dead-letter queue after max retries
});

await app.RunAsync();
```

## Configuration Options

### ServiceBusOptions Properties

```csharp
public class ServiceBusOptions
{
    /// <summary>
    /// Service Bus connection string (for development/connection string auth).
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Fully qualified namespace (for Managed Identity auth).
    /// Example: "myservicebus.servicebus.windows.net"
    /// </summary>
    public string? FullyQualifiedNamespace { get; set; }

    /// <summary>
    /// Topic name for publishing messages (default: "omniflow").
    /// </summary>
    public string TopicName { get; set; } = "omniflow";

    /// <summary>
    /// Service name used in subscription naming.
    /// </summary>
    public string ServiceName { get; set; } = "default";

    /// <summary>
    /// Optional subscription prefix (e.g., "prod").
    /// Results in: "prod-servicename-messagetype"
    /// </summary>
    public string? SubscriptionPrefix { get; set; }

    /// <summary>
    /// Enable sessions for ordered processing (default: true).
    /// Uses CorrelationId as SessionId.
    /// </summary>
    public bool EnableSessions { get; set; } = true;

    /// <summary>
    /// Maximum concurrent message handlers (default: 10).
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 10;

    /// <summary>
    /// Number of messages to prefetch (default: 0 = disabled).
    /// </summary>
    public int PrefetchCount { get; set; } = 0;

    /// <summary>
    /// Max delivery attempts before dead-lettering (default: 10).
    /// </summary>
    public int MaxDeliveryCount { get; set; } = 10;
}
```

### Advanced Configuration

```csharp
builder.Services.AddAzureServiceBusMessageBus(options =>
{
    options.FullyQualifiedNamespace = "production-bus.servicebus.windows.net";
    options.TopicName = "events";
    options.ServiceName = "payments-service";
    options.SubscriptionPrefix = "prod";
    
    // High throughput settings
    options.MaxConcurrentCalls = 50;
    options.PrefetchCount = 100;
    
    // Aggressive retry policy
    options.MaxDeliveryCount = 5;
    
    // Disable sessions for unordered high-throughput scenarios
    options.EnableSessions = false;
});
```

## Azure Setup

### 1. Create Service Bus Namespace

```bash
# Using Azure CLI
az servicebus namespace create \
  --name my-omniflow-bus \
  --resource-group my-resource-group \
  --location eastus \
  --sku Standard

# Get connection string
az servicebus namespace authorization-rule keys list \
  --resource-group my-resource-group \
  --namespace-name my-omniflow-bus \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString -o tsv
```

### 2. Create Topic

```bash
az servicebus topic create \
  --name omniflow \
  --namespace-name my-omniflow-bus \
  --resource-group my-resource-group \
  --enable-partitioning true \
  --enable-duplicate-detection true \
  --duplicate-detection-history-time-window PT10M
```

### 3. Create Subscriptions (Automatically created by adapter)

The adapter automatically creates subscriptions when you call `SubscribeAsync<T>`:

**Naming convention**: `{SubscriptionPrefix}-{ServiceName}-{MessageType}`

Example: `prod-orders-service-ordercreated`

### 4. Configure Subscription Filters (Optional)

Add SQL filters for message routing:

```bash
az servicebus topic subscription rule create \
  --resource-group my-resource-group \
  --namespace-name my-omniflow-bus \
  --topic-name omniflow \
  --subscription-name orders-service-ordercreated \
  --name OrderCreatedFilter \
  --filter-sql-expression "Subject = 'OrderCreated'"
```

## Sessions and Ordering

### Session-Based Processing (Default)

When `EnableSessions = true`:
- CorrelationId is used as SessionId
- Messages with the same correlation ID are processed in order
- Perfect for saga orchestration
- Each session is processed sequentially

```csharp
options.EnableSessions = true;
```

**Use case**: Order processing where steps must execute in sequence.

### Sessionless Processing (High Throughput)

When `EnableSessions = false`:
- Messages processed in parallel across all handlers
- No ordering guarantees
- Higher throughput

```csharp
options.EnableSessions = false;
options.MaxConcurrentCalls = 100;
options.PrefetchCount = 200;
```

**Use case**: Analytics events, logging, fire-and-forget notifications.

## Dead-Letter Queue Handling

Messages are automatically moved to the dead-letter queue after:
1. Exceeding `MaxDeliveryCount` retries
2. Processing throws an exception on final attempt

### Monitoring Dead-Letter Queue

```csharp
// Create a processor for the dead-letter queue
var client = new ServiceBusClient(connectionString);
var dlqProcessor = client.CreateProcessor(
    topicName: "omniflow",
    subscriptionName: "orders-service-ordercreated",
    options: new ServiceBusProcessorOptions
    {
        SubQueue = SubQueue.DeadLetter
    });

dlqProcessor.ProcessMessageAsync += async (args) =>
{
    var reason = args.Message.DeadLetterReason;
    var errorDescription = args.Message.DeadLetterErrorDescription;
    
    _logger.LogError("Dead-lettered: {Reason} - {Description}", reason, errorDescription);
    
    // Optionally republish or log for manual review
    await args.CompleteMessageAsync(args.Message);
};

await dlqProcessor.StartProcessingAsync();
```

### Replaying Dead-Letter Messages

```bash
# Using Service Bus Explorer or Azure CLI
az servicebus topic subscription rule create \
  --resource-group my-resource-group \
  --namespace-name my-omniflow-bus \
  --topic-name omniflow \
  --subscription-name orders-service-ordercreated \
  --name ReplayFilter
```

## Managed Identity Setup

### 1. Enable Managed Identity on App Service

```bash
az webapp identity assign \
  --name my-app \
  --resource-group my-resource-group
```

### 2. Grant Service Bus Permissions

```bash
# Get the principal ID
PRINCIPAL_ID=$(az webapp identity show \
  --name my-app \
  --resource-group my-resource-group \
  --query principalId -o tsv)

# Assign Azure Service Bus Data Sender role
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Azure Service Bus Data Sender" \
  --scope /subscriptions/{subscription-id}/resourceGroups/my-resource-group/providers/Microsoft.ServiceBus/namespaces/my-omniflow-bus

# Assign Azure Service Bus Data Receiver role
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Azure Service Bus Data Receiver" \
  --scope /subscriptions/{subscription-id}/resourceGroups/my-resource-group/providers/Microsoft.ServiceBus/namespaces/my-omniflow-bus
```

### 3. Use in Application

```csharp
builder.Services.AddAzureServiceBusMessageBus(options =>
{
    // No connection string needed!
    options.FullyQualifiedNamespace = "my-omniflow-bus.servicebus.windows.net";
    options.TopicName = "omniflow";
    options.ServiceName = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "default";
});
```

## Monitoring and Diagnostics

### Application Insights Integration

The adapter logs detailed telemetry:

```csharp
builder.Services.AddApplicationInsightsTelemetry();
```

Logged events:
- Message published (with MessageId, CorrelationId)
- Message processed successfully
- Processing errors with exception details
- Dead-letter queue movements

### Azure Monitor Metrics

Monitor these metrics in Azure Portal:
- **Incoming Messages**: Messages published to topic
- **Outgoing Messages**: Messages delivered to subscriptions
- **Active Messages**: Messages in queue
- **Dead-Letter Messages**: Messages in DLQ
- **Server Errors**: Service Bus throttling/errors

### Sample Query (Log Analytics)

```kusto
traces
| where customDimensions.CategoryName == "OmniFlow.Adapters.AzureServiceBus.AzureServiceBusMessageBus"
| where message contains "Error"
| project timestamp, message, customDimensions.CorrelationId, customDimensions.MessageId
| order by timestamp desc
```

## Performance Tuning

### High Throughput Configuration

```csharp
options.MaxConcurrentCalls = 100;
options.PrefetchCount = 200;
options.EnableSessions = false; // If ordering not required
```

**Throughput**: ~10,000 messages/sec (Standard tier) or ~100,000 messages/sec (Premium tier)

### Low Latency Configuration

```csharp
options.MaxConcurrentCalls = 10;
options.PrefetchCount = 0; // Disable prefetch
options.EnableSessions = true;
```

**Latency**: <10ms message delivery

### Resource Optimization

```csharp
options.MaxConcurrentCalls = 1;
options.PrefetchCount = 10;
```

**Use case**: Cost-sensitive scenarios, background processing

## Comparison with Other Adapters

| Feature | In-Memory | RabbitMQ | Kafka | Azure Service Bus |
|---------|-----------|----------|-------|-------------------|
| **Throughput** | High | Medium | Very High | High |
| **Sessions** | ❌ | ❌ | ⚠️ Partitions | ✅ Native |
| **Dead-Letter** | ❌ | ⚠️ Manual | ⚠️ Manual | ✅ Automatic |
| **Managed Service** | ❌ | ❌ | ❌ | ✅ |
| **Duplicate Detection** | ❌ | ❌ | ⚠️ Idempotent Producer | ✅ Native |
| **Cloud Integration** | ❌ | ❌ | ❌ | ✅ Azure |
| **Pricing** | Free | Self-hosted | Self-hosted | Pay-per-use |

## Cost Optimization

### Use Standard Tier for Development

```bash
az servicebus namespace create \
  --sku Standard \
  --name dev-omniflow-bus
```

**Cost**: ~$10/month + $0.05 per million operations

### Use Premium Tier for Production

```bash
az servicebus namespace create \
  --sku Premium \
  --capacity 1 \
  --name prod-omniflow-bus
```

**Cost**: ~$670/month for 1 messaging unit (dedicated resources)

**Benefits**:
- Predictable performance
- Network isolation (VNet integration)
- IP filtering
- Geo-disaster recovery

## Troubleshooting

### Connection Issues

**Problem**: "ServiceBusException: The remote name could not be resolved"

**Solution**:
1. Verify namespace: `ping myservicebus.servicebus.windows.net`
2. Check firewall rules
3. Verify Managed Identity has correct roles

### Messages Not Processing

**Problem**: Messages arrive but handlers not called

**Solution**:
1. Check subscription exists: `az servicebus topic subscription list`
2. Verify subscription filter rules
3. Check message Subject matches type name
4. Review dead-letter queue for failures

### High Latency

**Problem**: Slow message processing

**Solution**:
1. Increase `MaxConcurrentCalls` to 50+
2. Enable `PrefetchCount` (100-200)
3. Use Premium tier for dedicated capacity
4. Disable sessions if ordering not required

## Best Practices

1. **Use sessions for sagas**: Ensures ordered processing per correlation ID
2. **Monitor dead-letter queue**: Set up alerts for DLQ depth
3. **Use Managed Identity**: Avoid connection string rotation
4. **Enable duplicate detection**: MessageId-based deduplication
5. **Set appropriate TTL**: Configure message time-to-live on topic
6. **Use Premium tier for production**: Guaranteed throughput and SLA

## License

This adapter is part of the OmniFlow framework.
