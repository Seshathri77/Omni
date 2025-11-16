using Yath.ExpenseService.Models;

namespace Yath.ExpenseService.Repositories;

public interface ISettlementRepository
{
    Task<Settlement?> GetByIdAsync(string settlementId);
    Task<List<Settlement>> GetByTripIdAsync(string tripId);
    Task<List<Settlement>> GetPendingByUserAsync(string userId);
    Task CreateAsync(Settlement settlement);
    Task UpdateAsync(Settlement settlement);
}
