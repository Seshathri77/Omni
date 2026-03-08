using Microsoft.Extensions.DependencyInjection;

namespace OmniFlow.Core;

/// <summary>
/// Extension methods for registering OmniFlow.Core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OmniFlow core services to the dependency injection container.
    /// For backward compatibility. Consider using AddOmniFlow() instead.
    /// </summary>
    public static IServiceCollection AddOmniFlowCore(
        this IServiceCollection services,
        string? signingKey = null)
    {
        services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();

        if (!string.IsNullOrEmpty(signingKey))
        {
            services.AddSingleton<IMessageSigner>(sp => 
                new HmacMessageSigner(signingKey));
        }

        return services;
    }
}
