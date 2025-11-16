using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using OmniFlow.Core;
using OmniFlow.Idempotency;
using OmniFlow.Messaging;
using OmniFlow.Observability;
using OmniFlow.Sagas;
using Serilog;
using System.Text;
using Yath.ExpenseService.Models;
using Yath.ExpenseService.Repositories;
using Yath.ExpenseService.Sagas;
using Yath.Shared.Messages;

var builder = WebApplication.CreateBuilder(args);

// Serilog Configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "ExpenseService")
    .WriteTo.Console()
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341")
    .CreateLogger();

builder.Host.UseSerilog();

// MongoDB Configuration
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB") 
    ?? "mongodb://localhost:27017";
var mongoClient = new MongoClient(mongoConnectionString);
var mongoDatabase = mongoClient.GetDatabase("yath_expenses");

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(mongoDatabase);

// OmniFlow Core Services
builder.Services.AddOmniFlowCore();
builder.Services.AddOmniFlowMessaging();
builder.Services.AddOmniFlowSagas();
builder.Services.AddOmniFlowIdempotency();
builder.Services.AddOmniFlowObservability("ExpenseService");

// Repositories
builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
builder.Services.AddScoped<IExpenseGroupRepository, ExpenseGroupRepository>();
builder.Services.AddScoped<ISettlementRepository, SettlementRepository>();

// Sagas
builder.Services.AddSaga<ExpenseSettlementSaga, ExpenseSettlementSagaState>();

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
});

builder.Services.AddAuthorization();

// Controllers
builder.Services.AddControllers();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Yath Expense Service", 
        Version = "v1",
        Description = "Expense tracking and settlement service for Yath travel platform"
    });
    
    // JWT Bearer authentication in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
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

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
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

// Metrics endpoint
app.MapGet("/metrics", () => "Metrics endpoint - integrate with Prometheus");

// Subscribe to events
var messageBus = app.Services.GetRequiredService<IMessageBus>();
var serviceProvider = app.Services;

// Subscribe to TripCreated event to initialize expense groups
await messageBus.SubscribeAsync<TripCreated>(async (envelope, context) =>
{
    using var scope = serviceProvider.CreateScope();
    var groupRepository = scope.ServiceProvider.GetRequiredService<IExpenseGroupRepository>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Check if group already exists
        var existingGroup = await groupRepository.GetByTripIdAsync(envelope.Message.TripId);
        if (existingGroup != null)
        {
            logger.LogInformation("Expense group already exists for trip {TripId}", envelope.Message.TripId);
            return;
        }
        
        // Create new expense group
        var expenseGroup = new ExpenseGroup
        {
            GroupId = Guid.NewGuid().ToString(),
            TripId = envelope.Message.TripId,
            Members = new List<string> { envelope.Message.CreatorId },
            Currency = "USD"
        };
        
        expenseGroup.Balances[envelope.Message.CreatorId] = 0;
        
        await groupRepository.CreateAsync(expenseGroup);
        
        logger.LogInformation("Initialized expense group for trip {TripId}", envelope.Message.TripId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error initializing expense group for trip {TripId}", envelope.Message.TripId);
    }
});

// Subscribe to TripParticipantAdded
await messageBus.SubscribeAsync<TripParticipantAdded>(async (envelope, context) =>
{
    using var scope = serviceProvider.CreateScope();
    var groupRepository = scope.ServiceProvider.GetRequiredService<IExpenseGroupRepository>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        var group = await groupRepository.GetByTripIdAsync(envelope.Message.TripId);
        if (group == null)
        {
            logger.LogWarning("Expense group not found for trip {TripId}", envelope.Message.TripId);
            return;
        }
        
        if (!group.Members.Contains(envelope.Message.UserId))
        {
            group.Members.Add(envelope.Message.UserId);
            group.Balances[envelope.Message.UserId] = 0;
            
            await groupRepository.UpdateAsync(group);
            
            logger.LogInformation("Added user {UserId} to expense group for trip {TripId}", 
                envelope.Message.UserId, envelope.Message.TripId);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error adding participant to expense group");
    }
});

// Subscribe to TripParticipantRemoved
await messageBus.SubscribeAsync<TripParticipantRemoved>(async (envelope, context) =>
{
    using var scope = serviceProvider.CreateScope();
    var groupRepository = scope.ServiceProvider.GetRequiredService<IExpenseGroupRepository>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        var group = await groupRepository.GetByTripIdAsync(envelope.Message.TripId);
        if (group == null)
        {
            logger.LogWarning("Expense group not found for trip {TripId}", envelope.Message.TripId);
            return;
        }
        
        // Check outstanding balance
        if (group.Balances.TryGetValue(envelope.Message.UserId, out var balance) && Math.Abs(balance) > 0.01m)
        {
            logger.LogWarning("Cannot remove user {UserId} - outstanding balance: {Balance}", 
                envelope.Message.UserId, balance);
            return;
        }
        
        group.Members.Remove(envelope.Message.UserId);
        group.Balances.Remove(envelope.Message.UserId);
        
        await groupRepository.UpdateAsync(group);
        
        logger.LogInformation("Removed user {UserId} from expense group", envelope.Message.UserId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error removing participant from expense group");
    }
});

Log.Information("Expense Service started");

app.Run();
