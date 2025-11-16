using MongoDB.Driver;
using Yath.ExpenseService.Models;

namespace Yath.ExpenseService.Repositories;

public class ExpenseGroupRepository : IExpenseGroupRepository
{
    private readonly IMongoCollection<ExpenseGroup> _groups;

    public ExpenseGroupRepository(IMongoDatabase database)
    {
        _groups = database.GetCollection<ExpenseGroup>("expense_groups");

        // Create indexes
        var groupIdIndex = Builders<ExpenseGroup>.IndexKeys.Ascending(g => g.GroupId);
        _groups.Indexes.CreateOne(new CreateIndexModel<ExpenseGroup>(groupIdIndex,
            new CreateIndexOptions { Unique = true }));

        var tripIdIndex = Builders<ExpenseGroup>.IndexKeys.Ascending(g => g.TripId);
        _groups.Indexes.CreateOne(new CreateIndexModel<ExpenseGroup>(tripIdIndex,
            new CreateIndexOptions { Unique = true }));
    }

    public async Task<ExpenseGroup?> GetByIdAsync(string groupId)
    {
        return await _groups.Find(g => g.GroupId == groupId).FirstOrDefaultAsync();
    }

    public async Task<ExpenseGroup?> GetByTripIdAsync(string tripId)
    {
        return await _groups.Find(g => g.TripId == tripId).FirstOrDefaultAsync();
    }

    public async Task CreateAsync(ExpenseGroup group)
    {
        await _groups.InsertOneAsync(group);
    }

    public async Task UpdateAsync(ExpenseGroup group)
    {
        group.UpdatedAt = DateTime.UtcNow;
        await _groups.ReplaceOneAsync(g => g.GroupId == group.GroupId, group);
    }

    public async Task DeleteAsync(string groupId)
    {
        await _groups.DeleteOneAsync(g => g.GroupId == groupId);
    }
}
