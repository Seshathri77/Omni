using Microsoft.EntityFrameworkCore;
using OmniFlow.Sagas;
using System.Text.Json;

namespace OmniFlow.Adapters.Sql;

/// <summary>
/// SQL-based saga repository using Entity Framework.
/// </summary>
public class SqlSagaRepository<TState> : ISagaRepository<TState> where TState : SagaState
{
    private readonly OmniFlowDbContext _context;

    public SqlSagaRepository(OmniFlowDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(string sagaId, TState state, int version, CancellationToken cancellationToken = default)
    {
        var entity = await _context.SagaStates.FindAsync(new object[] { sagaId }, cancellationToken);

        var stateJson = JsonSerializer.Serialize(state);

        if (entity == null)
        {
            entity = new SagaStateEntity
            {
                SagaId = sagaId,
                SagaType = typeof(TState).Name,
                CorrelationId = state.CorrelationId,
                StateJson = stateJson,
                Version = version,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _context.SagaStates.Add(entity);
        }
        else
        {
            entity.StateJson = stateJson;
            entity.Version = version;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(TState State, int Version)?> GetAsync(string sagaId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.SagaStates.FindAsync(new object[] { sagaId }, cancellationToken);
        
        if (entity == null)
            return null;

        var state = JsonSerializer.Deserialize<TState>(entity.StateJson);
        if (state == null)
            return null;

        return (state, entity.Version);
    }

    public async Task DeleteAsync(string sagaId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.SagaStates.FindAsync(new object[] { sagaId }, cancellationToken);
        if (entity != null)
        {
            _context.SagaStates.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<string>> FindByCorrelationAsync(
        string propertyName,
        string value,
        CancellationToken cancellationToken = default)
    {
        // Simple implementation using CorrelationId
        return await _context.SagaStates
            .Where(s => s.CorrelationId == value)
            .Select(s => s.SagaId)
            .ToListAsync(cancellationToken);
    }
}
