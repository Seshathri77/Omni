using MongoDB.Driver;
using Yath.ExpenseService.Models;

namespace Yath.ExpenseService.Repositories;

public class ExpenseRepository : IExpenseRepository
{
    private readonly IMongoCollection<Expense> _expenses;

    public ExpenseRepository(IMongoDatabase database)
    {
        _expenses = database.GetCollection<Expense>("expenses");

        // Create indexes
        var expenseIdIndex = Builders<Expense>.IndexKeys.Ascending(e => e.ExpenseId);
        _expenses.Indexes.CreateOne(new CreateIndexModel<Expense>(expenseIdIndex,
            new CreateIndexOptions { Unique = true }));

        var tripIdIndex = Builders<Expense>.IndexKeys.Ascending(e => e.TripId);
        _expenses.Indexes.CreateOne(new CreateIndexModel<Expense>(tripIdIndex));

        var paidByIndex = Builders<Expense>.IndexKeys.Ascending(e => e.PaidBy);
        _expenses.Indexes.CreateOne(new CreateIndexModel<Expense>(paidByIndex));

        var dateIndex = Builders<Expense>.IndexKeys.Descending(e => e.Date);
        _expenses.Indexes.CreateOne(new CreateIndexModel<Expense>(dateIndex));
    }

    public async Task<Expense?> GetByIdAsync(string expenseId)
    {
        return await _expenses.Find(e => e.ExpenseId == expenseId).FirstOrDefaultAsync();
    }

    public async Task<List<Expense>> GetByTripIdAsync(string tripId, int skip = 0, int limit = 50)
    {
        return await _expenses.Find(e => e.TripId == tripId)
            .SortByDescending(e => e.Date)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<Expense>> GetByPaidByAsync(string userId, int skip = 0, int limit = 50)
    {
        return await _expenses.Find(e => e.PaidBy == userId)
            .SortByDescending(e => e.Date)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task CreateAsync(Expense expense)
    {
        await _expenses.InsertOneAsync(expense);
    }

    public async Task UpdateAsync(Expense expense)
    {
        expense.UpdatedAt = DateTime.UtcNow;
        await _expenses.ReplaceOneAsync(e => e.ExpenseId == expense.ExpenseId, expense);
    }

    public async Task DeleteAsync(string expenseId)
    {
        await _expenses.DeleteOneAsync(e => e.ExpenseId == expenseId);
    }
}
