using Yath.ExpenseService.Models;

namespace Yath.ExpenseService.Repositories;

public interface IExpenseGroupRepository
{
    Task<ExpenseGroup?> GetByIdAsync(string groupId);
    Task<ExpenseGroup?> GetByTripIdAsync(string tripId);
    Task CreateAsync(ExpenseGroup group);
    Task UpdateAsync(ExpenseGroup group);
    Task DeleteAsync(string groupId);
}
