using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace OmniFlow.Observability;

/// <summary>
/// Extension methods for registering OmniFlow.Observability services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OmniFlow observability with OpenTelemetry and Serilog.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceName">The name of the service for tracing.</param>
    /// <param name="configureTracing">Optional tracing configuration (e.g., OTLP exporter).</param>
    /// <param name="enablePrometheusExporter">Enable Prometheus /metrics endpoint. Default is true.</param>
    public static IServiceCollection AddOmniFlowObservability(
        this IServiceCollection services,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null,
        bool enablePrometheusExporter = true)
    {
        // Add metrics
        services.AddSingleton<OmniFlowMetrics>();

        // Configure OpenTelemetry tracing and metrics
        var otel = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(OmniFlowTelemetry.ActivitySourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddConsoleExporter();

                configureTracing?.Invoke(tracing);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(OmniFlowMetrics.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation();
                
                if (enablePrometheusExporter)
                {
                    metrics.AddPrometheusExporter();
                }
            });

        return services;
    }

    /// <summary>
    /// Maps the Prometheus scraping endpoint at /metrics.
    /// Call this in your application startup after app.Build().
    /// </summary>
    public static IApplicationBuilder UsePrometheusScrapingEndpoint(this IApplicationBuilder app)
    {
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
        return app;
    }

    /// <summary>
    /// Configures Serilog with OmniFlow enrichers.
    /// </summary>
    public static LoggerConfiguration AddOmniFlowEnrichers(
        this LoggerConfiguration loggerConfiguration,
        Core.ICorrelationAccessor correlationAccessor)
    {
        return loggerConfiguration
            .Enrich.With(new CorrelationIdEnricher(correlationAccessor))
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName();
    }

    /// <summary>
    /// Creates a default Serilog logger for OmniFlow.
    /// </summary>
    public static Serilog.ILogger CreateOmniFlowLogger(Core.ICorrelationAccessor correlationAccessor)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .AddOmniFlowEnrichers(correlationAccessor)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
