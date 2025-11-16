using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using OmniFlow.Adapters.MongoDb;
using OmniFlow.Adapters.RabbitMQ;
using OmniFlow.Core;
using OmniFlow.Idempotency;
using OmniFlow.Messaging;
using OmniFlow.Observability;
using OmniFlow.Sagas;
using Serilog;
using Yath.Shared.Messages;
using Yath.TripService.Repositories;
using Yath.TripService.Sagas;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// MongoDB setup
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB")!;
var mongoClient = new MongoClient(mongoConnectionString);
var mongoDatabase = mongoClient.GetDatabase("yath_trips");

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(mongoDatabase);

// OmniFlow Core Stack
builder.Services.AddOmniFlowCore();
builder.Services.AddOmniFlowObservability("Yath.TripService");
builder.Services.AddOmniFlowIdempotency();

// MongoDB Adapters for OmniFlow
builder.Services.AddMongoDbSagaRepository<TripCreationSagaState>(mongoConnectionString, "yath_trips");
builder.Services.AddMongoDbIdempotency(mongoConnectionString, "yath_trips");

// RabbitMQ Message Bus (or in-memory for local dev)
if (builder.Configuration["MessageBus:Provider"] == "RabbitMQ")
{
    builder.Services.AddRabbitMQMessageBus(options =>
    {
        options.HostName = builder.Configuration["RabbitMQ:HostName"]!;
        options.Port = int.Parse(builder.Configuration["RabbitMQ:Port"]!);
        options.UserName = builder.Configuration["RabbitMQ:UserName"]!;
        options.Password = builder.Configuration["RabbitMQ:Password"]!;
    });
}
else
{
    builder.Services.AddOmniFlowMessaging();
}

builder.Services.AddOmniFlowSagas();
builder.Services.AddSaga<TripCreationSaga, TripCreationSagaState>();

// Repositories
builder.Services.AddScoped<ITripRepository, TripRepository>();
builder.Services.AddScoped<IItineraryRepository, ItineraryRepository>();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(secretKey)
        };
    });

builder.Services.AddAuthorization();

// Controllers
builder.Services.AddControllers();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Yath Trip Service", Version = "v1" });
    
    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Yath.TripService" }));

// Metrics endpoint
app.MapGet("/metrics", () =>
{
    var metrics = new { service = "Yath.TripService", status = "available" };
    return Results.Ok(metrics);
});

// Subscribe to events
var messageBus = app.Services.GetRequiredService<IMessageBus>();

// Example: Listen to UserFollowed events to suggest trips
await messageBus.SubscribeAsync<UserFollowed>(async (envelope, context) =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("User {FollowerId} followed {FollowingId}", 
        envelope.Message.FollowerId, envelope.Message.FollowingId);
    // Could trigger trip recommendations based on follower relationships
    await Task.CompletedTask;
});

Log.Information("Yath.TripService started on {Environment}", app.Environment.EnvironmentName);

app.Run();
