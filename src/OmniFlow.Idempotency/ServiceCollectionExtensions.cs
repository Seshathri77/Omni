using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;

namespace OmniFlow.Idempotency;

/// <summary>
/// Extension methods for registering OmniFlow.Idempotency services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OmniFlow idempotency services with in-memory store.
    /// </summary>
    public static IServiceCollection AddOmniFlowIdempotency(
        this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        
        return services;
    }
}
