using ECommerce.OrderService.Extensions;
using ECommerce.OrderService.Sagas;
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
    
    options.ServiceName = config["ServiceName"] ?? "OrderService";
    options.EnableSagas = config.GetValue<bool>("EnableSagas", true);
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

    // Register sagas
    options.RegisterSaga<OrderSaga, OrderSagaState>();
});

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

// Subscribe to saga events using OmniFlow framework
await app.SubscribeToSagaEventsAsync();

app.Logger.LogInformation("Order Service starting with {Provider} message bus", 
    builder.Configuration["OmniFlow:MessageBus:Provider"] ?? "InMemory");

await app.RunAsync();
