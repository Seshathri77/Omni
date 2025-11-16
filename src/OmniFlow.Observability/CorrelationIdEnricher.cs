using OmniFlow.Core;
using Serilog.Core;
using Serilog.Events;

namespace OmniFlow.Observability;

/// <summary>
/// Serilog enricher that adds correlation ID to log events.
/// </summary>
public class CorrelationIdEnricher : ILogEventEnricher
{
    private readonly ICorrelationAccessor _correlationAccessor;

    public CorrelationIdEnricher(ICorrelationAccessor correlationAccessor)
    {
        _correlationAccessor = correlationAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = _correlationAccessor.CorrelationId;
        if (!string.IsNullOrEmpty(correlationId))
        {
            var property = propertyFactory.CreateProperty("CorrelationId", correlationId);
            logEvent.AddPropertyIfAbsent(property);
        }

        var causationId = _correlationAccessor.CausationId;
        if (!string.IsNullOrEmpty(causationId))
        {
            var property = propertyFactory.CreateProperty("CausationId", causationId);
            logEvent.AddPropertyIfAbsent(property);
        }
    }
}
