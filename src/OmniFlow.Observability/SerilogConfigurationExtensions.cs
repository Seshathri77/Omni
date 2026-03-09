using Serilog;
using Serilog.Configuration;

namespace OmniFlow.Observability;

/// <summary>
/// Extension methods for Serilog configuration
/// </summary>
public static class SerilogConfigurationExtensions
{
    /// <summary>
    /// Enriches log events with OmniFlow correlation ID
    /// </summary>
    public static LoggerConfiguration WithOmniFlowCorrelationId(
        this LoggerEnrichmentConfiguration enrichmentConfiguration,
        Core.ICorrelationAccessor correlationAccessor)
    {
        if (enrichmentConfiguration == null)
            throw new ArgumentNullException(nameof(enrichmentConfiguration));

        return enrichmentConfiguration.With(new CorrelationIdEnricher(correlationAccessor));
    }
}
