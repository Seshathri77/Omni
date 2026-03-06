# OmniFlow.Adapters.Kafka

Apache Kafka adapter for OmniFlow message bus, providing production-grade distributed messaging with high throughput and fault tolerance.

## Features

- ✅ **High Throughput**: Kafka's partitioned log architecture for massive scale
- ✅ **Ordered Processing**: Messages with same correlation ID go to same partition
- ✅ **Reliable Delivery**: Manual offset commits ensure at-least-once delivery
- ✅ **Idempotent Producer**: Built-in deduplication prevents duplicate sends
- ✅ **Consumer Groups**: Multiple instances for parallel processing
- ✅ **Topic-per-Message-Type**: Automatic topic naming and creation
- ✅ **Configurable**: Full access to Confluent.Kafka producer/consumer settings

## Installation

```bash
dotnet add package OmniFlow.Adapters.Kafka
```

## Quick Start

### Basic Configuration

```csharp
using OmniFlow.Adapters.Kafka;
using OmniFlow.Core;
using OmniFlow.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Register OmniFlow core services
builder.Services.AddOmniFlowCore();

// Use Kafka instead of in-memory message bus
builder.Services.AddKafkaMessageBus(options =>
{
    options.BootstrapServers = "localhost:9092";
    options.ClientId = "orders-service";
    options.ConsumerGroupId = "orders-service-group";
    options.TopicPrefix = "prod"; // Optional: results in topics like "prod.ordercreated"
});

var app = builder.Build();
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
    
    // Offset is committed automatically after successful processing
});

await app.RunAsync();
```

## Configuration Options

### KafkaOptions Properties

```csharp
public class KafkaOptions
{
    /// <summary>
    /// Kafka bootstrap servers (e.g., "localhost:9092" or "broker1:9092,broker2:9092").
    /// </summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// Client identifier for this application.
    /// </summary>
    public string ClientId { get; set; } = "omniflow-client";

    /// <summary>
    /// Consumer group ID for coordinated consumption.
    /// </summary>
    public string ConsumerGroupId { get; set; } = "omniflow-group";

    /// <summary>
    /// Optional topic prefix (e.g., "prod" results in topics like "prod.ordercreated").
    /// </summary>
    public string? TopicPrefix { get; set; }

    /// <summary>
    /// Additional producer configuration.
    /// </summary>
    public Dictionary<string, string>? ProducerConfig { get; set; }

    /// <summary>
    /// Additional consumer configuration.
    /// </summary>
    public Dictionary<string, string>? ConsumerConfig { get; set; }
}
```

### Advanced Configuration

```csharp
builder.Services.AddKafkaMessageBus(options =>
{
    options.BootstrapServers = "broker1:9092,broker2:9092,broker3:9092";
    options.ClientId = "payments-service";
    options.ConsumerGroupId = "payments-service-group";
    options.TopicPrefix = "production";

    // Custom producer settings
    options.ProducerConfig = new Dictionary<string, string>
    {
        { "compression.type", "snappy" },
        { "linger.ms", "10" }, // Batch messages for 10ms
        { "batch.size", "100000" }
    };

    // Custom consumer settings
    options.ConsumerConfig = new Dictionary<string, string>
    {
        { "fetch.min.bytes", "10000" },
        { "max.partition.fetch.bytes", "1048576" } // 1MB
    };
});
```

## Topic Naming Convention

Topics are automatically named based on message type:

- **Without prefix**: `ordercreated`, `paymentsuccesseeded`
- **With prefix** (`TopicPrefix = "prod"`): `prod.ordercreated`, `prod.paymentsuccesseeded`

## Message Partitioning

Messages are partitioned by **CorrelationId** to ensure:
- All related messages (same saga/workflow) go to the same partition
- Ordered processing within a correlation context
- Parallel processing across different correlation contexts

## Offset Management

- **Manual commits**: Offsets committed only after successful message processing
- **At-least-once delivery**: Messages reprocessed if handler throws exception
- **Idempotency recommended**: Use `IIdempotencyStore` to handle duplicate deliveries

## Consumer Groups

Multiple instances of the same service automatically coordinate via consumer groups:

```
Service Instance 1 → Partition 0, 1
Service Instance 2 → Partition 2, 3
Service Instance 3 → Partition 4, 5
```

Each partition is consumed by exactly one instance, providing parallel processing with ordering guarantees.

## Running Kafka Locally

### Using Docker

```bash
# Start Kafka with Zookeeper
docker run -d --name zookeeper -p 2181:2181 zookeeper:3.8

docker run -d --name kafka -p 9092:9092 \
  -e KAFKA_ZOOKEEPER_CONNECT=zookeeper:2181 \
  -e KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://localhost:9092 \
  -e KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR=1 \
  --link zookeeper \
  confluentinc/cp-kafka:7.5.0
```

### Using Docker Compose

```yaml
version: '3.8'
services:
  zookeeper:
    image: confluentinc/cp-zookeeper:7.5.0
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
    ports:
      - "2181:2181"

  kafka:
    image: confluentinc/cp-kafka:7.5.0
    depends_on:
      - zookeeper
    ports:
      - "9092:9092"
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://localhost:9092
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 1
      KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 1
```

## Production Considerations

### 1. Replication

Set replication factor for fault tolerance:

```csharp
options.ProducerConfig = new Dictionary<string, string>
{
    { "acks", "all" }, // Wait for all replicas
    { "min.insync.replicas", "2" } // Require 2 replicas
};
```

### 2. Monitoring

Monitor consumer lag using Kafka's consumer groups:

```bash
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group orders-service-group --describe
```

### 3. Error Handling

Implement dead-letter topic for persistent failures:

```csharp
await messageBus.SubscribeAsync<OrderCreated>(async (envelope, context) =>
{
    try
    {
        await ProcessOrder(envelope.Message);
    }
    catch (Exception ex)
    {
        // After N retries, publish to dead-letter topic
        await messageBus.PublishAsync(new OrderProcessingFailed
        {
            OriginalMessage = envelope,
            Error = ex.Message
        });
    }
});
```

### 4. Schema Registry (Optional)

For schema evolution, integrate with Confluent Schema Registry:

```bash
dotnet add package Confluent.SchemaRegistry
dotnet add package Confluent.SchemaRegistry.Serdes.Json
```

## Comparison with Other Adapters

| Feature | In-Memory | RabbitMQ | Kafka | Azure Service Bus |
|---------|-----------|----------|-------|-------------------|
| **Throughput** | High | Medium | Very High | High |
| **Durability** | ❌ None | ✅ Disk | ✅ Replicated Log | ✅ Cloud |
| **Ordering** | ✅ | ⚠️ Per Queue | ✅ Per Partition | ✅ Per Session |
| **Retention** | ❌ | ⚠️ Limited | ✅ Configurable | ✅ Configurable |
| **Replay** | ❌ | ❌ | ✅ | ⚠️ Limited |
| **Use Case** | Development | Traditional Messaging | Event Streaming | Azure Cloud |

## Troubleshooting

### Consumer Not Receiving Messages

1. **Check topic exists**:
   ```bash
   kafka-topics --bootstrap-server localhost:9092 --list
   ```

2. **Verify consumer group**:
   ```bash
   kafka-consumer-groups --bootstrap-server localhost:9092 --list
   ```

3. **Check consumer lag**:
   ```bash
   kafka-consumer-groups --bootstrap-server localhost:9092 \
     --group your-group-id --describe
   ```

### Performance Tuning

For high throughput, adjust batch settings:

```csharp
options.ProducerConfig = new Dictionary<string, string>
{
    { "linger.ms", "100" },      // Wait up to 100ms to batch messages
    { "batch.size", "1000000" }, // Batch up to 1MB
    { "compression.type", "lz4" } // Compress batches
};
```

## License

This adapter is part of the OmniFlow framework.
