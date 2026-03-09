using ECommerce.PaymentService.Extensions;
using ECommerce.PaymentService.Services;
using Microsoft.Extensions.Hosting;
using OmniFlow.Core;
using OmniFlow.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure OmniFlow from appsettings.json
builder.Services.AddOmniFlow(options =>
{
    var config = builder.Configuration.GetSection("OmniFlow");
    
    options.ServiceName = config["ServiceName"] ?? "PaymentService";
    options.EnableSagas = config.GetValue<bool>("EnableSagas", false);
    options.EnableIdempotency = config.GetValue<bool>("EnableIdempotency", true);
    options.EnableObservability = config.GetValue<bool>("EnableObservability", true);
    
    // Message Bus Configuration
    var messageBusConfig = config.GetSection("MessageBus");
    var provider = messageBusConfig["Provider"] ?? "InMemory";
    options.MessageBus.Provider = Enum.Parse<MessageBusProvider>(provider, true);
    
    if (options.MessageBus.Provider == MessageBusProvider.RabbitMQ)
    {
        var rabbitConfig = messageBusConfig.GetSection("RabbitMQ");
        options.MessageBus.RabbitMQ = new RabbitMQConfig
        {
            HostName = rabbitConfig["HostName"] ?? "localhost",
            Port = rabbitConfig.GetValue<int>("Port", 5672),
            UserName = rabbitConfig["UserName"] ?? "guest",
            Password = rabbitConfig["Password"] ?? "guest",
            VirtualHost = rabbitConfig["VirtualHost"] ?? "/",
            ExchangeName = rabbitConfig["ExchangeName"] ?? "ecommerce-exchange"
        };
    }

    // Observability Configuration
    var obsConfig = config.GetSection("Observability");
    if (obsConfig.Exists())
    {
        options.Observability.OtlpEndpoint = obsConfig["OtlpEndpoint"];
        options.Observability.EnablePrometheusExporter = obsConfig.GetValue<bool>("EnablePrometheusExporter", true);
    }
});

// Add payment processor and handlers
builder.Services.AddSingleton<IPaymentProcessor, SimulatedPaymentProcessor>();
builder.Services.AddPaymentHandlers();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// Map default health check endpoints
app.MapDefaultEndpoints();

// Subscribe to payment commands using extension methods
await app.SubscribeToPaymentCommandsAsync();

app.Logger.LogInformation("Payment Service starting with {Provider} message bus", 
    builder.Configuration["OmniFlow:MessageBus:Provider"] ?? "InMemory");

await app.RunAsync();
