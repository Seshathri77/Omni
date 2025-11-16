# OmniFlow Framework - AI Agent Instructions

## Architecture Overview

**OmniFlow** is a microservices framework built on .NET 8 providing saga orchestration, distributed tracing, and message-driven architecture. The framework follows a layered, modular design with clear separation of concerns.

### Core Architectural Patterns

1. **Message-Driven Architecture**: All inter-service communication uses message envelopes (`MessageEnvelope<T>`) wrapping commands/events with correlation metadata
2. **Saga Orchestration Pattern**: Long-running business transactions managed by `Saga<TState>` base class with automatic state persistence and compensation
3. **Middleware Pipeline**: Messages flow through configurable middleware (correlation, logging, retry) before reaching handlers
4. **Repository Pattern**: Abstract storage via `ISagaRepository<TState>` and `IIdempotencyStore` with in-memory and SQL implementations

### Project Dependency Graph

```
OmniFlow.Core (foundation)
  ↓
OmniFlow.Messaging → OmniFlow.Idempotency
  ↓                        ↓
OmniFlow.Sagas ← OmniFlow.Observability
  ↓
OmniFlow.Adapters.* (RabbitMQ, SQL)
```

**Key Rule**: Core projects have NO dependencies on adapters. Adapters depend on abstractions only.

## Framework Conventions

### Message Design

- **Commands** (imperative): `CreateOrder`, `RequestPayment` - implement `ICommand`
- **Events** (past tense): `OrderCreated`, `PaymentSucceeded` - implement `IEvent`
- **Always use records**: `public record OrderCreated(string OrderId, decimal Amount) : IEvent;`
- **MessageEnvelope wraps everything**: Contains `MessageId`, `CorrelationId`, `CausationId`, `Timestamp`, `SchemaVersion`

### Saga Implementation Pattern

```csharp
// 1. Define state inheriting from SagaState
public class OrderSagaState : SagaState
{
    public string OrderId { get; set; } = string.Empty;
    public bool PaymentCompleted { get; set; }
}

// 2. Inherit from Saga<TState>
public class OrderSaga : Saga<OrderSagaState>
{
    // 3. Override lifecycle hooks
    protected override async Task OnStartAsync(CancellationToken ct)
    {
        // Publish commands using PublishAsync
        await PublishAsync(new RequestPayment(...), ct);
    }

    protected override async Task OnCompensateAsync(CancellationToken ct)
    {
        // Rollback logic
        await PublishAsync(new CancelOrder(...), ct);
    }

    // 4. Create event handlers that load state first
    public async Task HandlePaymentSucceeded(PaymentSucceeded evt, CancellationToken ct)
    {
        State.PaymentCompleted = true;
        await CompleteAsync(ct); // Marks saga completed
    }
}
```

### Correlation Context Flow

**Critical**: Correlation must flow through all operations:
1. `ICorrelationAccessor` uses `AsyncLocal<T>` for thread-safe context
2. `CorrelationMiddleware` sets context from `MessageEnvelope`
3. All logs automatically include `CorrelationId` via `CorrelationIdEnricher`
4. New messages inherit correlation: `MessageEnvelope<T>.Create(message, correlationAccessor)`

### Dependency Injection Registration

Services register via extension methods on `IServiceCollection`:

```csharp
// Core stack (always together)
services.AddOmniFlowCore();           // ICorrelationAccessor, IMessageSigner
services.AddOmniFlowMessaging();      // IMessageBus (in-memory), middleware
services.AddOmniFlowSagas();          // ISagaRepository (in-memory), ITimerService
services.AddOmniFlowIdempotency();    // IIdempotencyStore (in-memory)
services.AddOmniFlowObservability("ServiceName"); // OpenTelemetry, Serilog

// Swap adapters for production
services.AddRabbitMQMessageBus(opts => opts.HostName = "rabbit");
services.AddOmniFlowSqlAdapters(connectionString);

// Register individual sagas
services.AddSaga<OrderSaga, OrderSagaState>();
```

## Critical Implementation Details

### Saga State Persistence

- **Optimistic concurrency**: `Version` field prevents conflicts
- **History tracking**: `State.History` list for debugging (auto-populated by base class)
- **Status transitions**: `Running → Completed` OR `Running → Compensating → Compensated`
- **SaveStateAsync called automatically** by `PublishAsync`, `CompleteAsync`, `CompensateAsync`

### Message Bus Lifecycle

```csharp
// Subscribe (typically in Program.cs after app.Build())
await messageBus.SubscribeAsync<OrderCreated>(async (envelope, context) =>
{
    // Initialize saga
    var saga = sp.GetRequiredService<OrderSaga>();
    saga.Initialize(repository, messageBus, timerService);
    
    // Load existing state OR start new
    if (!await saga.LoadAsync(sagaId))
        await saga.StartAsync(correlationId);
    
    // Handle event
    await saga.HandleEvent(envelope.Message);
});

// Publish
await messageBus.PublishAsync(new OrderCreated(...));
```

### Idempotency Pattern

Wrap message handlers:
```csharp
await messageBus.SubscribeAsync<OrderCreated>(async (envelope, context) =>
{
    if (!await idempotencyStore.TryRecordAsync(envelope.MessageId, "OrderService"))
        return; // Already processed
    
    // Process message
});
```

## Testing Patterns

### Unit Testing Sagas

```csharp
[Fact]
public async Task Should_Complete_Saga_On_Payment_Success()
{
    // Arrange: Use in-memory implementations
    var repository = new InMemorySagaRepository<OrderSagaState>();
    var messageBus = new InMemoryMessageBus(accessor, logger);
    var saga = new OrderSaga();
    saga.Initialize(repository, messageBus);
    
    // Act: Start and handle events
    await saga.StartAsync("correlation-123");
    await saga.HandlePaymentSucceeded(new PaymentSucceeded(...));
    
    // Assert: Check state
    var state = await repository.GetAsync(saga.State.SagaId);
    state!.Value.State.Status.Should().Be(SagaStatus.Completed);
}
```

### Integration Testing with Message Bus

Use `InMemoryMessageBus` and verify published messages:
```csharp
var publishedMessages = new List<object>();
await messageBus.SubscribeAsync<PaymentRequested>((env, ctx) =>
{
    publishedMessages.Add(env.Message);
    return Task.CompletedTask;
});
```

## Common Patterns & Gotchas

### ✅ DO

- Always use `await PublishAsync()` inside sagas (never direct MessageBus usage)
- Check `State.Status` before processing events (avoid double-processing)
- Use `protected` methods in saga base class for encapsulation
- Initialize sagas with `saga.Initialize()` before any operations
- Use `CancellationToken` parameters throughout async methods

### ❌ DON'T

- Never mutate `State` without calling a save method (`PublishAsync`, `CompleteAsync`, etc.)
- Don't create sagas as singletons (they're stateful - use transient lifetime)
- Avoid synchronous I/O in message handlers (breaks async pipeline)
- Don't swallow exceptions in saga handlers (let middleware handle retries)
- Never share `Saga<TState>` instances across messages

## File Organization

```
/src/ProjectName/
  ├── Messages/           # All ICommand, IEvent records
  │   └── OrderMessages.cs
  ├── Sagas/             # Saga implementations
  │   ├── OrderSaga.cs
  │   └── OrderSagaState.cs
  ├── Controllers/       # ASP.NET Core endpoints (for commands)
  └── Program.cs         # DI registration + message subscriptions
```

## OpenTelemetry Integration

Activities created automatically for:
- Message processing (via `OmniFlowTelemetry.StartMessageActivity`)
- Saga operations (via `OmniFlowTelemetry.StartSagaActivity`)

Tags added: `messaging.correlation_id`, `saga.id`, `saga.operation`

## SQL Adapter Schema

When using `OmniFlow.Adapters.Sql`:

**SagaStates Table**:
- `SagaId` (PK)
- `SagaType`, `CorrelationId` (indexed)
- `StateJson` (serialized state)
- `Version` (concurrency)

**IdempotencyRecords Table**:
- Composite PK: `(MessageId, ConsumerName)`
- `ProcessedAt`, `ExpiresAt`

Run EF migrations: `dotnet ef migrations add InitialCreate`

## Building & Running

```bash
# Restore and build
dotnet restore
dotnet build

# Run tests
dotnet test

# Run sample services
cd samples/OrdersService && dotnet run
cd samples/PaymentsService && dotnet run

# Test flow
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"amount": 99.99, "customerId": "cust-123"}'
```

## Code Generation Shortcuts

When asked to create a new saga:
1. Create `{Name}SagaState : SagaState` with business properties
2. Create `{Name}Saga : Saga<{Name}SagaState>` with event handlers
3. Define messages in `Messages/{Name}Messages.cs`
4. Register in `Program.cs`: `services.AddSaga<{Name}Saga, {Name}SagaState>()`
5. Subscribe to triggering events in `Program.cs`

## Key Files Reference

- **Core abstractions**: `src/OmniFlow.Core/ICorrelationAccessor.cs`, `MessageEnvelope.cs`
- **Message bus**: `src/OmniFlow.Messaging/IMessageBus.cs`, `InMemoryMessageBus.cs`
- **Saga engine**: `src/OmniFlow.Sagas/Saga.cs`, `ISagaRepository.cs`
- **Example saga**: `samples/OrdersService/Sagas/OrderSaga.cs`
- **Test examples**: `tests/OmniFlow.Tests/Sagas/SagaTests.cs`
