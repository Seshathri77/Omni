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

// Add observability with Jaeger exporter and Prometheus metrics
builder.Services.AddOmniFlowObservability("PaymentsService", 
    tracing =>
    {
        tracing.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317"); // Jaeger OTLP endpoint
        });
    },
    enablePrometheusExporter: true); // Exposes /metrics endpoint

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
