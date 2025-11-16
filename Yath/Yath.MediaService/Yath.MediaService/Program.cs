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
using Yath.MediaService.Repositories;
using Yath.MediaService.Services;

var builder = WebApplication.CreateBuilder(args);

// Serilog Configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "MediaService")
    .WriteTo.Console()
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341")
    .CreateLogger();

builder.Host.UseSerilog();

// MongoDB Configuration
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB") 
    ?? "mongodb://localhost:27017";
var mongoClient = new MongoClient(mongoConnectionString);
var mongoDatabase = mongoClient.GetDatabase("yath_media");

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(mongoDatabase);

// Azure Blob Storage
var azureStorageConnectionString = builder.Configuration.GetConnectionString("AzureStorage") 
    ?? "UseDevelopmentStorage=true";
var containerName = builder.Configuration["BlobStorage:ContainerName"] ?? "yath-media";

builder.Services.AddSingleton<IBlobStorageService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<AzureBlobStorageService>>();
    return new AzureBlobStorageService(azureStorageConnectionString, containerName, logger);
});

// OmniFlow Core Services
builder.Services.AddOmniFlowCore();
builder.Services.AddOmniFlowMessaging();
builder.Services.AddOmniFlowIdempotency();
builder.Services.AddOmniFlowObservability("MediaService");

// Repositories
builder.Services.AddScoped<IMediaRepository, MediaRepository>();

// Services
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();

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
        Title = "Yath Media Service", 
        Version = "v1",
        Description = "Media upload and management service for Yath travel platform"
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

Log.Information("Media Service started");

app.Run();
