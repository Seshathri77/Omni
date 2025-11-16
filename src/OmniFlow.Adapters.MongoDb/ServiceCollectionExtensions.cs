using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using OmniFlow.Idempotency;
using OmniFlow.Sagas;

namespace OmniFlow.Adapters.MongoDb;

/// <summary>
/// Extension methods for registering MongoDB adapters.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MongoDB-based idempotency store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">MongoDB connection string.</param>
    /// <param name="databaseName">Database name.</param>
    /// <param name="collectionName">Collection name for idempotency records. Default is "idempotency_records".</param>
    public static IServiceCollection AddMongoDbIdempotency(
        this IServiceCollection services,
        string connectionString,
        string databaseName,
        string collectionName = "idempotency_records")
    {
        services.AddSingleton<IMongoClient>(sp => new MongoClient(connectionString));
        services.AddSingleton<IIdempotencyStore>(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var database = client.GetDatabase(databaseName);
            return new MongoDbIdempotencyStore(database, collectionName);
        });

        return services;
    }

    /// <summary>
    /// Adds MongoDB-based saga repository for a specific saga state type.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">MongoDB connection string.</param>
    /// <param name="databaseName">Database name.</param>
    /// <param name="collectionName">Collection name for saga states. Default is "saga_states".</param>
    public static IServiceCollection AddMongoDbSagaRepository<TState>(
        this IServiceCollection services,
        string connectionString,
        string databaseName,
        string collectionName = "saga_states")
        where TState : class
    {
        services.AddSingleton<IMongoClient>(sp => new MongoClient(connectionString));
        services.AddSingleton<ISagaRepository<TState>>(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var database = client.GetDatabase(databaseName);
            return new MongoDbSagaRepository<TState>(database, collectionName);
        });

        return services;
    }

    /// <summary>
    /// Adds MongoDB adapters for both idempotency and saga repository.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">MongoDB connection string.</param>
    /// <param name="databaseName">Database name.</param>
    public static IServiceCollection AddOmniFlowMongoDbAdapters<TState>(
        this IServiceCollection services,
        string connectionString,
        string databaseName)
        where TState : class
    {
        services.AddMongoDbIdempotency(connectionString, databaseName);
        services.AddMongoDbSagaRepository<TState>(connectionString, databaseName);

        return services;
    }
}
