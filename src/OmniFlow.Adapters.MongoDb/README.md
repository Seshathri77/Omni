# OmniFlow.Adapters.MongoDb

MongoDB adapters for OmniFlow framework providing distributed idempotency store and saga repository implementations.

## Features

- **MongoDB Idempotency Store**: Distributed idempotency tracking with automatic TTL cleanup
- **MongoDB Saga Repository**: Saga state persistence with optimistic concurrency control
- **Automatic Indexing**: Efficient queries with proper MongoDB indexes
- **TTL Support**: Automatic cleanup of expired idempotency records

## Installation

```bash
dotnet add package OmniFlow.Adapters.MongoDb
```

## Quick Start

### 1. Add MongoDB Idempotency Store

```csharp
using OmniFlow.Adapters.MongoDb;

var builder = WebApplication.CreateBuilder(args);

// Add MongoDB idempotency store
builder.Services.AddMongoDbIdempotency(
    connectionString: "mongodb://localhost:27017",
    databaseName: "omniflow",
    collectionName: "idempotency_records" // optional, default shown
);
```

### 2. Add MongoDB Saga Repository

```csharp
using OmniFlow.Adapters.MongoDb;
using OmniFlow.Sagas;

// Add MongoDB saga repository for a specific saga state type
builder.Services.AddMongoDbSagaRepository<OrderSagaState>(
    connectionString: "mongodb://localhost:27017",
    databaseName: "omniflow",
    collectionName: "saga_states" // optional, default shown
);
```

### 3. Add Both (Idempotency + Saga Repository)

```csharp
// Add both MongoDB adapters at once
builder.Services.AddOmniFlowMongoDbAdapters<OrderSagaState>(
    connectionString: "mongodb://localhost:27017",
    databaseName: "omniflow"
);
```

## Configuration

### Connection String Formats

**Local MongoDB:**
```
mongodb://localhost:27017
```

**MongoDB with Authentication:**
```
mongodb://username:password@localhost:27017
```

**MongoDB Atlas:**
```
mongodb+srv://username:password@cluster.mongodb.net/
```

**Replica Set:**
```
mongodb://host1:27017,host2:27017,host3:27017/?replicaSet=myReplicaSet
```

### Configuration from appsettings.json

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "omniflow",
    "IdempotencyCollection": "idempotency_records",
    "SagaCollection": "saga_states"
  }
}
```

```csharp
var mongoConfig = builder.Configuration.GetSection("MongoDB");

builder.Services.AddOmniFlowMongoDbAdapters<OrderSagaState>(
    mongoConfig["ConnectionString"]!,
    mongoConfig["DatabaseName"]!
);
```

## Usage

### Idempotency Store

```csharp
var idempotencyStore = app.Services.GetRequiredService<IIdempotencyStore>();

// Check and record message processing
if (await idempotencyStore.TryRecordAsync(messageId, "OrdersService"))
{
    // Process message (first time)
    await ProcessOrderAsync(order);
}
else
{
    // Message already processed, skip
    logger.LogInformation("Duplicate message {MessageId} detected", messageId);
}

// Check if message was processed
bool exists = await idempotencyStore.ExistsAsync(messageId, "OrdersService");

// Remove record (for testing)
await idempotencyStore.RemoveAsync(messageId, "OrdersService");
```

### Custom TTL (Time-To-Live)

```csharp
// Record with custom expiration (1 hour)
await idempotencyStore.TryRecordAsync(
    messageId, 
    "OrdersService", 
    ttl: TimeSpan.FromHours(1)
);
```

### Saga Repository

```csharp
var sagaRepository = app.Services.GetRequiredService<ISagaRepository<OrderSagaState>>();

// Save saga state
var state = new OrderSagaState 
{ 
    SagaId = Guid.NewGuid().ToString(),
    OrderId = "order-123",
    Amount = 99.99m
};
await sagaRepository.SaveAsync(state);

// Load saga state
var loadedState = await sagaRepository.GetAsync(state.SagaId);

// Find by correlation ID
var foundState = await sagaRepository.GetByCorrelationIdAsync(correlationId);

// List sagas with filtering
var mongoRepo = (MongoDbSagaRepository<OrderSagaState>)sagaRepository;
var runningSagas = await mongoRepo.ListAsync(status: SagaStatus.Running, limit: 10);
```

## MongoDB Indexes

### Idempotency Collection

**Indexes created automatically:**
1. **Compound Unique Index**: `(consumerName, messageId)` - Ensures atomic idempotency
2. **TTL Index**: `expiresAt` - Automatic cleanup of expired records

**Document Structure:**
```json
{
  "_id": ObjectId("..."),
  "messageId": "msg-123",
  "consumerName": "OrdersService",
  "processedAt": ISODate("2025-11-16T10:30:00Z"),
  "expiresAt": ISODate("2025-11-23T10:30:00Z")
}
```

### Saga Collection

**Indexes created automatically:**
1. **Unique Index**: `sagaId` - Fast lookup by saga ID
2. **Index**: `correlationId` - Query by correlation ID
3. **Index**: `sagaType` - Filter by saga type
4. **Index**: `status` - Filter by saga status

**Document Structure:**
```json
{
  "_id": ObjectId("..."),
  "sagaId": "order-123",
  "correlationId": "correlation-abc",
  "sagaType": "OrderSagaState",
  "status": "Running",
  "version": 3,
  "createdAt": ISODate("2025-11-16T10:30:00Z"),
  "updatedAt": ISODate("2025-11-16T10:31:00Z"),
  "state": {
    "sagaId": "order-123",
    "correlationId": "correlation-abc",
    "orderId": "order-123",
    "amount": 99.99,
    "paymentCompleted": false,
    "status": "Running",
    "version": 3,
    "history": ["Started", "Payment requested"],
    "createdAt": ISODate("2025-11-16T10:30:00Z")
  }
}
```

## Concurrency Control

The MongoDB saga repository uses **optimistic concurrency** via version numbers:

```csharp
// Version is automatically incremented on each save
state.Version++; // Handled by base Saga class
await sagaRepository.SaveAsync(state);

// If another process updated the saga, SaveAsync throws:
// InvalidOperationException: "Saga state version mismatch..."
```

**Best Practice**: Wrap saga operations in try-catch and retry on version conflicts.

## Manual Cleanup

The idempotency store includes a manual cleanup method (TTL index handles this automatically):

```csharp
var mongoStore = (MongoDbIdempotencyStore)idempotencyStore;
await mongoStore.CleanupExpiredRecordsAsync();
```

## MongoDB Setup

### Using Docker

```bash
# Start MongoDB
docker run -d --name mongodb -p 27017:27017 mongo:latest

# Start with authentication
docker run -d --name mongodb \
  -e MONGO_INITDB_ROOT_USERNAME=admin \
  -e MONGO_INITDB_ROOT_PASSWORD=password \
  -p 27017:27017 \
  mongo:latest
```

### Connection String with Auth

```csharp
builder.Services.AddOmniFlowMongoDbAdapters<OrderSagaState>(
    connectionString: "mongodb://admin:password@localhost:27017",
    databaseName: "omniflow"
);
```

## Testing

Use in-memory MongoDB for testing:

```bash
dotnet add package Mongo2Go
```

```csharp
using Mongo2Go;

// In test setup
var runner = MongoDbRunner.Start();
var connectionString = runner.ConnectionString;

services.AddOmniFlowMongoDbAdapters<OrderSagaState>(
    connectionString,
    "omniflow_test"
);

// In test teardown
runner.Dispose();
```

## Production Considerations

### 1. Connection Pooling

MongoDB driver handles connection pooling automatically. Configure via connection string:

```
mongodb://localhost:27017/?maxPoolSize=100&minPoolSize=10
```

### 2. Write Concern

For critical operations, use majority write concern:

```csharp
var client = new MongoClient(new MongoClientSettings
{
    Server = new MongoServerAddress("localhost", 27017),
    WriteConcern = WriteConcern.WMajority
});
```

### 3. Read Preference

For read-heavy workloads with replica sets:

```csharp
var settings = MongoClientSettings.FromConnectionString(connectionString);
settings.ReadPreference = ReadPreference.SecondaryPreferred;
var client = new MongoClient(settings);
```

### 4. Monitoring

Monitor MongoDB performance:
- Track slow queries: `db.currentOp()`
- Check index usage: `db.collection.stats()`
- Monitor replication lag in replica sets

### 5. Backup Strategy

```bash
# Backup database
mongodump --db omniflow --out /backup

# Restore database
mongorestore --db omniflow /backup/omniflow
```

## Troubleshooting

### Duplicate Key Errors

If you see duplicate key errors for idempotency:
- ✅ This is expected behavior (prevents duplicate processing)
- The `TryRecordAsync` method returns `false` for duplicates

### Version Mismatch Errors

If sagas throw version mismatch errors:
- This indicates concurrent updates
- Implement retry logic in your saga handlers
- Consider reducing concurrency or using pessimistic locking

### Index Creation Fails

If indexes fail to create:
- Ensure MongoDB user has `createIndex` permission
- Check for existing data that violates unique constraints
- Drop and recreate collection if needed

## Comparison with SQL Adapter

| Feature | MongoDB | SQL Server |
|---------|---------|------------|
| Schema | Flexible (JSON) | Fixed (Tables) |
| Indexing | Automatic | Manual (EF migrations) |
| Concurrency | Version-based | Version-based |
| TTL Cleanup | Native support | Custom job required |
| Transactions | Multi-document | Full ACID |
| Scalability | Horizontal (sharding) | Vertical (scale-up) |

## References

- [MongoDB Driver Documentation](https://www.mongodb.com/docs/drivers/csharp/)
- [MongoDB .NET Tutorial](https://www.mongodb.com/languages/csharp)
- [MongoDB Best Practices](https://www.mongodb.com/docs/manual/administration/production-notes/)
