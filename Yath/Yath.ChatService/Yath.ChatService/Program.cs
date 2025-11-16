using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using OmniFlow.Core;
using OmniFlow.Idempotency;
using OmniFlow.Messaging;
using OmniFlow.Observability;
using Serilog;
using System.Text;
using Yath.ChatService.Hubs;
using Yath.ChatService.Models;
using Yath.ChatService.Repositories;
using Yath.Shared.Messages;

var builder = WebApplication.CreateBuilder(args);

// Serilog Configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "ChatService")
    .WriteTo.Console()
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341")
    .CreateLogger();

builder.Host.UseSerilog();

// MongoDB Configuration
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB") 
    ?? "mongodb://localhost:27017";
var mongoClient = new MongoClient(mongoConnectionString);
var mongoDatabase = mongoClient.GetDatabase("yath_chat");

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(mongoDatabase);

// OmniFlow Core Services
builder.Services.AddOmniFlowCore();
builder.Services.AddOmniFlowMessaging();
builder.Services.AddOmniFlowIdempotency();
builder.Services.AddOmniFlowObservability("ChatService");

// Repositories
builder.Services.AddScoped<IChatRoomRepository, ChatRoomRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IPresenceRepository, PresenceRepository>();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "your-super-secret-key-change-in-production-min-32-chars";
var jwtKey = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(jwtKey),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "yath-api",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "yath-users",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
    
    // SignalR authentication
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
            {
                context.Token = accessToken;
            }
            
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// SignalR
builder.Services.AddSignalR();

// Controllers
builder.Services.AddControllers();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Yath Chat Service", 
        Version = "v1",
        Description = "Real-time messaging service with SignalR for Yath travel platform"
    });
    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
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

// Health checks
builder.Services.AddHealthChecks();

// CORS (important for SignalR)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000") // Add your frontend URLs
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseRouting();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<ChatHub>("/hubs/chat");

// Metrics endpoint
app.MapGet("/metrics", () => "Metrics endpoint - integrate with Prometheus");

// Subscribe to events
var messageBus = app.Services.GetRequiredService<IMessageBus>();
var serviceProvider = app.Services;

// Subscribe to CreateChatRoom command
await messageBus.SubscribeAsync<CreateChatRoom>(async (envelope, context) =>
{
    using var scope = serviceProvider.CreateScope();
    var roomRepository = scope.ServiceProvider.GetRequiredService<IChatRoomRepository>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Check if room already exists
        var existingRoom = await roomRepository.GetByTripIdAsync(envelope.Message.TripId);
        if (existingRoom != null)
        {
            logger.LogInformation("Chat room already exists for trip {TripId}", envelope.Message.TripId);
            return;
        }
        
        var chatRoom = new ChatRoom
        {
            RoomId = Guid.NewGuid().ToString(),
            TripId = envelope.Message.TripId,
            ParticipantIds = envelope.Message.ParticipantIds.ToList()
        };
        
        await roomRepository.CreateAsync(chatRoom);
        
        // Publish event
        await messageBus.PublishAsync(new ChatRoomCreated(
            chatRoom.RoomId,
            chatRoom.TripId,
            chatRoom.ParticipantIds,
            DateTime.UtcNow
        ));
        
        logger.LogInformation("Created chat room {RoomId} for trip {TripId}", chatRoom.RoomId, chatRoom.TripId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating chat room for trip {TripId}", envelope.Message.TripId);
    }
});

// Subscribe to TripParticipantAdded
await messageBus.SubscribeAsync<TripParticipantAdded>(async (envelope, context) =>
{
    using var scope = serviceProvider.CreateScope();
    var roomRepository = scope.ServiceProvider.GetRequiredService<IChatRoomRepository>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        var room = await roomRepository.GetByTripIdAsync(envelope.Message.TripId);
        if (room == null)
        {
            logger.LogWarning("Chat room not found for trip {TripId}", envelope.Message.TripId);
            return;
        }
        
        await roomRepository.AddParticipantAsync(room.RoomId, envelope.Message.UserId);
        
        logger.LogInformation("Added user {UserId} to chat room {RoomId}", 
            envelope.Message.UserId, room.RoomId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error adding participant to chat room");
    }
});

// Subscribe to TripParticipantRemoved
await messageBus.SubscribeAsync<TripParticipantRemoved>(async (envelope, context) =>
{
    using var scope = serviceProvider.CreateScope();
    var roomRepository = scope.ServiceProvider.GetRequiredService<IChatRoomRepository>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        var room = await roomRepository.GetByTripIdAsync(envelope.Message.TripId);
        if (room == null)
        {
            logger.LogWarning("Chat room not found for trip {TripId}", envelope.Message.TripId);
            return;
        }
        
        await roomRepository.RemoveParticipantAsync(room.RoomId, envelope.Message.UserId);
        
        logger.LogInformation("Removed user {UserId} from chat room {RoomId}", 
            envelope.Message.UserId, room.RoomId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error removing participant from chat room");
    }
});

Log.Information("Chat Service started");

app.Run();
