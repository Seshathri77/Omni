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
using Yath.UserService.Repositories;
using Yath.UserService.Sagas;
using Yath.UserService.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq(builder.Configuration["Seq:Url"] ?? "http://localhost:5341")
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Yath User Service", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
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

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();

// Configure MongoDB
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB") 
    ?? "mongodb://localhost:27017";
var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"] ?? "yath_users";

builder.Services.AddSingleton<IMongoClient>(sp => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(mongoDatabaseName);
});

// Register repositories
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IUserConnectionRepository, UserConnectionRepository>();

// Register services
builder.Services.AddSingleton<IAuthService, AuthService>();

// Configure OmniFlow
builder.Services.AddOmniFlowCore();
builder.Services.AddOmniFlowMessaging();
builder.Services.AddOmniFlowSagas();
builder.Services.AddOmniFlowIdempotency();
builder.Services.AddOmniFlowObservability("user-service", enablePrometheusExporter: true);

// Use MongoDB adapters
builder.Services.AddMongoDbIdempotency(mongoConnectionString, mongoDatabaseName);
builder.Services.AddMongoDbSagaRepository<UserRegistrationSagaState>(mongoConnectionString, mongoDatabaseName);

// Use RabbitMQ for messaging (will switch to Azure Service Bus in production)
var rabbitMqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
builder.Services.AddRabbitMQMessageBus(options =>
{
    options.HostName = rabbitMqHost;
    options.UserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest";
    options.Password = builder.Configuration["RabbitMQ:Password"] ?? "guest";
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "*" })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
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

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map Prometheus metrics endpoint
app.UsePrometheusScrapingEndpoint();

// Subscribe to events (if needed for this service)
var messageBus = app.Services.GetRequiredService<IMessageBus>();
// Add event subscriptions here if needed

app.Run();
