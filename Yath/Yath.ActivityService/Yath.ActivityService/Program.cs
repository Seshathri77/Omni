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
using Serilog;
using Yath.Shared.Messages;
using Yath.ActivityService.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// MongoDB setup
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB")!;
var mongoClient = new MongoClient(mongoConnectionString);
var mongoDatabase = mongoClient.GetDatabase("yath_activity");

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(mongoDatabase);

// OmniFlow Core Stack
builder.Services.AddOmniFlowCore();
builder.Services.AddOmniFlowObservability("Yath.ActivityService");
builder.Services.AddOmniFlowIdempotency();

// MongoDB Adapters for OmniFlow
builder.Services.AddMongoDbIdempotency(mongoConnectionString, "yath_activity");

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

// Repositories
builder.Services.AddScoped<IPostRepository, PostRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<ILikeRepository, LikeRepository>();

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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Yath Activity Service", Version = "v1" });
    
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
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Yath.ActivityService" }));

// Metrics endpoint
app.MapGet("/metrics", () =>
{
    var metrics = new { service = "Yath.ActivityService", status = "available" };
    return Results.Ok(metrics);
});

// Subscribe to events
var messageBus = app.Services.GetRequiredService<IMessageBus>();
var postRepository = app.Services.CreateScope().ServiceProvider.GetRequiredService<IPostRepository>();

// Listen to TripCreated events to auto-publish to feed
await messageBus.SubscribeAsync<TripCreated>(async (envelope, context) =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Trip {TripId} created, auto-creating feed post", envelope.Message.TripId);
    
    // Auto-create a post for the trip
    var post = new Yath.ActivityService.Models.Post
    {
        PostId = Guid.NewGuid().ToString(),
        UserId = envelope.Message.CreatorId,
        Content = $"Started planning a trip: {envelope.Message.Title}",
        TripId = envelope.Message.TripId,
        Tags = new List<string> { "trip" },
        Visibility = Yath.ActivityService.Models.PostVisibility.Public
    };
    
    var scope = app.Services.CreateScope();
    var repo = scope.ServiceProvider.GetRequiredService<IPostRepository>();
    await repo.CreateAsync(post);
    await Task.CompletedTask;
});

// Listen to TripStatusUpdated events
await messageBus.SubscribeAsync<TripStatusUpdated>(async (envelope, context) =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Trip {TripId} status updated to {Status}", 
        envelope.Message.TripId, envelope.Message.NewStatus);
    // Could auto-post status updates to feed
});

Log.Information("Yath.ActivityService started on {Environment}", app.Environment.EnvironmentName);

app.Run();
