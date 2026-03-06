using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace OmniFlow.Sagas;

/// <summary>
/// SQL-based distributed lock implementation.
/// </summary>
public class SqlDistributedLock : IDistributedLock
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SqlDistributedLock> _logger;

    public SqlDistributedLock(
        IServiceProvider serviceProvider,
        ILogger<SqlDistributedLock> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<IAsyncDisposable?> AcquireAsync(
        string key,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var scope = _serviceProvider.CreateScope();
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<ISagaDbContext>();
            var expiresAt = DateTimeOffset.UtcNow.Add(timeout);

            var lockRecord = new DistributedLockEntity
            {
                LockKey = key,
                AcquiredAt = DateTimeOffset.UtcNow,
                ExpiresAt = expiresAt,
                Owner = Environment.MachineName + ":" + Environment.ProcessId
            };

            try
            {
                context.DistributedLocks.Add(lockRecord);
                await context.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Acquired distributed lock: {LockKey}", key);
                return new LockHandle(key, this, scope);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Lock already held by another instance
                _logger.LogDebug("Failed to acquire lock (already held): {LockKey}", key);
                scope.Dispose();
                return null;
            }
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ISagaDbContext>();

        // Check for non-expired locks
        var exists = await context.DistributedLocks
            .AnyAsync(l => l.LockKey == key && l.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);

        return exists;
    }

    public async Task ReleaseAsync(string key, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ISagaDbContext>();

        var lockRecord = await context.DistributedLocks.FindAsync(new object[] { key }, cancellationToken);
        if (lockRecord != null)
        {
            context.DistributedLocks.Remove(lockRecord);
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Released distributed lock: {LockKey}", key);
        }
    }

    private bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // Check for SQL Server unique constraint violation
        if (ex.InnerException is SqlException sqlEx)
        {
            // Error 2627: Violation of PRIMARY KEY or UNIQUE constraint
            // Error 2601: Cannot insert duplicate key
            return sqlEx.Number == 2627 || sqlEx.Number == 2601;
        }
        return false;
    }

    private class LockHandle : IAsyncDisposable
    {
        private readonly string _key;
        private readonly SqlDistributedLock _lock;
        private readonly IServiceScope _scope;

        public LockHandle(string key, SqlDistributedLock distributedLock, IServiceScope scope)
        {
            _key = key;
            _lock = distributedLock;
            _scope = scope;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _lock.ReleaseAsync(_key);
            }
            finally
            {
                _scope.Dispose();
            }
        }
    }
}
