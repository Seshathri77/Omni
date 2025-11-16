using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using OmniFlow.Core;
using OmniFlow.Idempotency;
using OmniFlow.Messaging;
using OmniFlow.Observability;
using Serilog;
using Yath.LocationService.Hubs;
using Yath.LocationService.Repositories;
using Yath.Shared.Messages;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog (will be enriched with correlation after services are built)
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.With(new CorrelationIdEnricher(services.GetRequiredService<ICorrelationAccessor>())));

// MongoDB
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb") 
    ?? "mongodb://localhost:27017";
var mongoClient = new MongoClient(mongoConnectionString);
var database = mongoClient.GetDatabase("yath_location");

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(database);

// OmniFlow services
builder.Services.AddOmniFlowCore();
builder.Services.AddOmniFlowMessaging();
builder.Services.AddOmniFlowIdempotency();
builder.Services.AddOmniFlowObservability("LocationService");

// Repositories
builder.Services.AddSingleton<ILocationUpdateRepository, LocationUpdateRepository>();
builder.Services.AddSingleton<ITrackingSessionRepository, TrackingSessionRepository>();
builder.Services.AddSingleton<ILocationHistoryRepository, LocationHistoryRepository>();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "your-super-secret-key-change-in-production";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "YathLocationService";
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

        // For SignalR - allow token from query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/location"))
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

// CORS - allow SignalR from web clients
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR
    });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Yath Location Service", Version = "v1" });
    
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
app.MapHub<LocationHub>("/hubs/location");
app.MapHealthChecks("/health");

// Subscribe to events
var messageBus = app.Services.GetRequiredService<IMessageBus>();
var idempotencyStore = app.Services.GetRequiredService<IIdempotencyStore>();

// Subscribe to location-related events if needed
// Example: TripCreated to auto-initialize tracking for trip participants

app.Logger.LogInformation("Location Service started on {Urls}", builder.Configuration["Urls"]);
app.Run();
