using MongoDB.Driver;
using Yath.ExpenseService.Models;

namespace Yath.ExpenseService.Repositories;

public class SettlementRepository : ISettlementRepository
{
    private readonly IMongoCollection<Settlement> _settlements;

    public SettlementRepository(IMongoDatabase database)
    {
        _settlements = database.GetCollection<Settlement>("settlements");

        // Create indexes
        var settlementIdIndex = Builders<Settlement>.IndexKeys.Ascending(s => s.SettlementId);
        _settlements.Indexes.CreateOne(new CreateIndexModel<Settlement>(settlementIdIndex,
            new CreateIndexOptions { Unique = true }));

        var tripIdIndex = Builders<Settlement>.IndexKeys.Ascending(s => s.TripId);
        _settlements.Indexes.CreateOne(new CreateIndexModel<Settlement>(tripIdIndex));

        var statusIndex = Builders<Settlement>.IndexKeys.Ascending(s => s.Status);
        _settlements.Indexes.CreateOne(new CreateIndexModel<Settlement>(statusIndex));
    }

    public async Task<Settlement?> GetByIdAsync(string settlementId)
    {
        return await _settlements.Find(s => s.SettlementId == settlementId).FirstOrDefaultAsync();
    }

    public async Task<List<Settlement>> GetByTripIdAsync(string tripId)
    {
        return await _settlements.Find(s => s.TripId == tripId)
            .SortByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Settlement>> GetPendingByUserAsync(string userId)
    {
        var filter = Builders<Settlement>.Filter.And(
            Builders<Settlement>.Filter.Or(
                Builders<Settlement>.Filter.Eq(s => s.From, userId),
                Builders<Settlement>.Filter.Eq(s => s.To, userId)
            ),
            Builders<Settlement>.Filter.Eq(s => s.Status, SettlementStatus.Pending)
        );

        return await _settlements.Find(filter)
            .SortByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task CreateAsync(Settlement settlement)
    {
        await _settlements.InsertOneAsync(settlement);
    }

    public async Task UpdateAsync(Settlement settlement)
    {
        await _settlements.ReplaceOneAsync(s => s.SettlementId == settlement.SettlementId, settlement);
    }
}
