using Yath.ExpenseService.Models;

namespace Yath.ExpenseService.Repositories;

public interface IExpenseRepository
{
    Task<Expense?> GetByIdAsync(string expenseId);
    Task<List<Expense>> GetByTripIdAsync(string tripId, int skip = 0, int limit = 50);
    Task<List<Expense>> GetByPaidByAsync(string userId, int skip = 0, int limit = 50);
    Task CreateAsync(Expense expense);
    Task UpdateAsync(Expense expense);
    Task DeleteAsync(string expenseId);
}
