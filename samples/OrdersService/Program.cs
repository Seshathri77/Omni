using OmniFlow.Core;
using OmniFlow.Messaging;
using OmniFlow.Sagas;
using OmniFlow.Idempotency;
using OmniFlow.Observability;
using OmniFlow.Adapters.RabbitMQ;
using OrdersService.Messages;
using OrdersService.Sagas;
using Serilog;
using Serilog.Events;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with Seq
var correlationAccessor = new CorrelationAccessor();
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.With(new OmniFlow.Observability.CorrelationIdEnricher(correlationAccessor))
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();
builder.Host.UseSerilog();

// Add OmniFlow services
builder.Services.AddOmniFlowCore();
builder.Services.AddRabbitMQMessageBus(options =>
{
    options.HostName = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";
    options.Port = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672");
    options.UserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest";
    options.Password = builder.Configuration["RabbitMQ:Password"] ?? "guest";
    options.VirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/";
});
builder.Services.AddOmniFlowSagas().AddSaga<OrderSaga, OrderSagaState>();
builder.Services.AddOmniFlowIdempotency();
// Add observability with Jaeger exporter and Prometheus metrics
builder.Services.AddOmniFlowObservability("OrdersService", 
    tracing =>
    {
        tracing.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317"); // Jaeger OTLP endpoint
        });
    },
    enablePrometheusExporter: true); // Exposes /metrics endpoint

// Register saga
//builder.Services.AddSaga<OrderSaga, OrderSagaState>();

// Add controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable Prometheus scraping endpoint at /metrics
app.UsePrometheusScrapingEndpoint();

// Subscribe to events
var messageBus = app.Services.GetRequiredService<IMessageBus>();
var idempotencyStore = app.Services.GetRequiredService<OmniFlow.Idempotency.IIdempotencyStore>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Handle OrderCreated - start saga (with idempotency check)
await messageBus.SubscribeAsync<OrderCreated>(async (envelope, context) =>
{
    var messageId = envelope.MessageId;
    
    // Check idempotency
    if (!await idempotencyStore.TryRecordAsync(messageId, "OrdersService-OrderCreated"))
    {
        logger.LogInformation("Duplicate message {MessageId} detected, skipping", messageId);
        return;
    }
    
    logger.LogInformation("Processing new message {MessageId} for order {OrderId}", 
        messageId, envelope.Message.OrderId);
    
    // Process message using saga
    var sagaRepo = app.Services.GetRequiredService<OmniFlow.Sagas.ISagaRepository<OrderSagaState>>();
    var timerService = app.Services.GetRequiredService<OmniFlow.Sagas.ITimerService>();
    
    var saga = new OrderSaga();
    saga.Initialize(sagaRepo, messageBus, timerService);
    await saga.StartOrderAsync(envelope.Message, CancellationToken.None);
});

// Handle PaymentSucceeded
await messageBus.SubscribeSagaContinue<OrderSaga, OrderSagaState, PaymentSucceeded>(
    app.Services,
    msg => msg.OrderId,
    (saga, msg, ct) => saga.HandlePaymentSucceededAsync(msg, ct));

// Handle PaymentFailed
await messageBus.SubscribeSagaContinue<OrderSaga, OrderSagaState, PaymentFailed>(
    app.Services,
    msg => msg.OrderId,
    (saga, msg, ct) => saga.HandlePaymentFailedAsync(msg, ct));

app.MapControllers();

app.Run();
