using OmniFlow.Core;
using OmniFlow.Messaging;
using OmniFlow.Observability;
using OmniFlow.Adapters.RabbitMQ;
using PaymentsService.Messages;
using OrdersService.Messages;
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

// Add OmniFlow services - Unified configuration
builder.Services.AddOmniFlow(options =>
{
    options.ServiceName = "PaymentsService";

    // Message Bus configuration
    options.MessageBus.Provider = MessageBusProvider.RabbitMQ;
    options.MessageBus.RabbitMQ = new RabbitMQConfig
    {
        HostName = builder.Configuration["RabbitMQ:HostName"] ?? "localhost",
        Port = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672"),
        UserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest",
        Password = builder.Configuration["RabbitMQ:Password"] ?? "guest",
        VirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/",
        ExchangeName = "omniflow"
    };

    // Enable features (PaymentsService doesn't use sagas in this example)
    options.EnableSagas = false;
    options.EnableIdempotency = false;
    options.EnableObservability = true;

    // Observability configuration
    options.Observability.EnablePrometheusExporter = true;
    options.Observability.OtlpEndpoint = "http://localhost:4317"; // Jaeger OTLP endpoint
});

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

// Subscribe to PaymentRequested events
var messageBus = app.Services.GetRequiredService<IMessageBus>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

await messageBus.SubscribeAsync<PaymentRequested>(async (envelope, context) =>
{
    var payment = envelope.Message;
    logger.LogInformation("Processing payment for order {OrderId}, amount {Amount}", 
        payment.OrderId, payment.Amount);

    // Simulate payment processing
    await Task.Delay(1000);

    // Randomly succeed or fail (80% success rate for demo)
    var random = new Random();
    var success = random.Next(100) < 80;

    if (success)
    {
        var paymentId = Guid.NewGuid().ToString();
        await messageBus.PublishAsync(new OrdersService.Messages.PaymentSucceeded(payment.OrderId, paymentId));
        logger.LogInformation("Payment succeeded for order {OrderId}", payment.OrderId);
    }
    else
    {
        await messageBus.PublishAsync(new OrdersService.Messages.PaymentFailed(payment.OrderId, "Insufficient funds"));
        logger.LogWarning("Payment failed for order {OrderId}", payment.OrderId);
    }
});

app.MapControllers();

app.Run();
