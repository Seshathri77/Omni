using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OmniFlow.Idempotency;
using OmniFlow.Sagas;
using OmniFlow.Messaging;

namespace OmniFlow.Adapters.Sql;

/// <summary>
/// Extension methods for registering SQL adapters.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds SQL persistence for sagas and idempotency.
    /// </summary>
    public static IServiceCollection AddOmniFlowSqlAdapters(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<OmniFlowDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Register DbContext as ISagaDbContext for timer and lock services
        services.AddScoped<ISagaDbContext>(sp => sp.GetRequiredService<OmniFlowDbContext>());

        services.AddScoped(typeof(ISagaRepository<>), typeof(SqlSagaRepository<>));
        services.AddScoped<IIdempotencyStore, SqlIdempotencyStore>();
        services.AddScoped<IDeadLetterQueueStore, SqlDeadLetterQueueStore>();

        return services;
    }
}
