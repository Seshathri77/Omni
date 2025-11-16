using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using OmniFlow.Core;
using OmniFlow.Idempotency;
using OmniFlow.Messaging;
using OmniFlow.Observability;
using Serilog;
using Yath.NotificationService.Models;
using Yath.NotificationService.Repositories;
using Yath.NotificationService.Services;
using Yath.Shared.Messages;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.With(new CorrelationIdEnricher(services.GetRequiredService<ICorrelationAccessor>())));

// MongoDB
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb") 
    ?? "mongodb://localhost:27017";
var mongoClient = new MongoClient(mongoConnectionString);
var database = mongoClient.GetDatabase("yath_notifications");

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(database);

// OmniFlow services
builder.Services.AddOmniFlowCore();
builder.Services.AddOmniFlowMessaging();
builder.Services.AddOmniFlowIdempotency();
builder.Services.AddOmniFlowObservability("NotificationService");

// Repositories
builder.Services.AddSingleton<INotificationRepository, NotificationRepository>();
builder.Services.AddSingleton<IDeviceTokenRepository, DeviceTokenRepository>();
builder.Services.AddSingleton<INotificationPreferenceRepository, NotificationPreferenceRepository>();

// Services
builder.Services.AddSingleton<IFcmService, FcmService>();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "your-super-secret-key-change-in-production";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "YathNotificationService";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "YathUsers";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

// Controllers
builder.Services.AddControllers();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Yath Notification Service", Version = "v1" });
    
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// Subscribe to events from other services
var messageBus = app.Services.GetRequiredService<IMessageBus>();
var idempotencyStore = app.Services.GetRequiredService<IIdempotencyStore>();
var notificationRepository = app.Services.GetRequiredService<INotificationRepository>();
var deviceTokenRepository = app.Services.GetRequiredService<IDeviceTokenRepository>();
var preferenceRepository = app.Services.GetRequiredService<INotificationPreferenceRepository>();
var fcmService = app.Services.GetRequiredService<IFcmService>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Helper method to send notification
async Task SendNotificationToUser(string userId, NotificationType type, string title, string body, 
    Dictionary<string, string>? payload = null, string? imageUrl = null, string? relatedEntityId = null, string? relatedEntityType = null)
{
    // Check user preferences
    var preference = await preferenceRepository.GetByUserIdAsync(userId);
    if (preference != null && !preference.EnablePushNotifications)
    {
        logger.LogDebug("User {UserId} has push notifications disabled", userId);
        return;
    }

    // Check specific notification type preference
    if (preference != null)
    {
        var enabled = type switch
        {
            NotificationType.TripInvite => preference.TripInvites,
            NotificationType.TripUpdate => preference.TripUpdates,
            NotificationType.NewMessage => preference.Messages,
            NotificationType.NewComment => preference.Comments,
            NotificationType.NewLike => preference.Likes,
            NotificationType.NewFollower => preference.Followers,
            NotificationType.ExpenseAdded => preference.Expenses,
            NotificationType.ExpenseSettlement => preference.Expenses,
            NotificationType.LocationShared => preference.LocationSharing,
            NotificationType.MediaTagged => preference.MediaTagging,
            NotificationType.TripReminder => preference.TripReminders,
            NotificationType.System => preference.SystemNotifications,
            _ => true
        };

        if (!enabled)
        {
            logger.LogDebug("User {UserId} has {Type} notifications disabled", userId, type);
            return;
        }
    }

    // Create notification record
    var notification = new Notification
    {
        UserId = userId,
        Type = type,
        Title = title,
        Body = body,
        Payload = payload ?? new Dictionary<string, string>(),
        ImageUrl = imageUrl,
        RelatedEntityId = relatedEntityId,
        RelatedEntityType = relatedEntityType
    };

    await notificationRepository.CreateAsync(notification);

    // Send push notification to all active devices
    var devices = await deviceTokenRepository.GetActiveByUserIdAsync(userId);
    if (devices.Any() && fcmService.IsInitialized)
    {
        foreach (var device in devices)
        {
            try
            {
                await fcmService.SendNotificationAsync(device, notification);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send push notification to device {TokenId}", device.TokenId);
            }
        }
    }
}

// Subscribe to UserFollowed event
await messageBus.SubscribeAsync<UserFollowed>(async (envelope, context) =>
{
    if (!await idempotencyStore.TryRecordAsync(envelope.MessageId, "NotificationService"))
        return;

    var msg = envelope.Message;
    await SendNotificationToUser(
        msg.FollowingId,
        NotificationType.NewFollower,
        "New Follower",
        $"You have a new follower!",
        new Dictionary<string, string> { { "followerId", msg.FollowerId } },
        relatedEntityId: msg.FollowerId,
        relatedEntityType: "user"
    );
});

// Subscribe to TripParticipantAdded event (trip invites)
await messageBus.SubscribeAsync<TripParticipantAdded>(async (envelope, context) =>
{
    if (!await idempotencyStore.TryRecordAsync(envelope.MessageId, "NotificationService"))
        return;

    var msg = envelope.Message;
    await SendNotificationToUser(
        msg.UserId,
        NotificationType.TripInvite,
        "Trip Invitation",
        $"You've been added to a trip!",
        new Dictionary<string, string> { { "tripId", msg.TripId } },
        relatedEntityId: msg.TripId,
        relatedEntityType: "trip"
    );
});

// Subscribe to MessageSent event
await messageBus.SubscribeAsync<MessageSent>(async (envelope, context) =>
{
    if (!await idempotencyStore.TryRecordAsync(envelope.MessageId, "NotificationService"))
        return;

    var msg = envelope.Message;
    // Note: In production, you'd fetch trip participants and send to all except sender
    logger.LogInformation("New message in room {RoomId} from {UserId}", msg.RoomId, msg.UserId);
});

// Subscribe to ActivityLiked event
await messageBus.SubscribeAsync<ActivityLiked>(async (envelope, context) =>
{
    if (!await idempotencyStore.TryRecordAsync(envelope.MessageId, "NotificationService"))
        return;

    var msg = envelope.Message;
    // Note: Would need to fetch post author from Activity Service
    logger.LogInformation("Activity {ActivityId} liked by {LikedBy}", msg.ActivityId, msg.LikedBy);
});

// Subscribe to CommentAdded event
await messageBus.SubscribeAsync<CommentAdded>(async (envelope, context) =>
{
    if (!await idempotencyStore.TryRecordAsync(envelope.MessageId, "NotificationService"))
        return;

    var msg = envelope.Message;
    // Note: Would need to fetch post author from Activity Service
    logger.LogInformation("Comment added to activity {ActivityId} by {UserId}", msg.ActivityId, msg.UserId);
});

// Subscribe to ExpenseAdded event
await messageBus.SubscribeAsync<ExpenseAdded>(async (envelope, context) =>
{
    if (!await idempotencyStore.TryRecordAsync(envelope.MessageId, "NotificationService"))
        return;

    var msg = envelope.Message;
    logger.LogInformation("Expense added to trip {TripId}", msg.TripId);
});

app.Logger.LogInformation("Notification Service started on {Urls}", builder.Configuration["Urls"]);
app.Run();
