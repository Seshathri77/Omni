using OmniFlow.Sagas;
using Yath.Shared.Messages;

namespace Yath.ExpenseService.Sagas;

public class ExpenseSettlementSaga : Saga<ExpenseSettlementSagaState>
{
    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        // Generate settlement recommendations
        var settlements = CalculateSettlements(State.Balances);
        
        foreach (var settlement in settlements)
        {
            // Publish settlement created events
            await PublishAsync(new SettlementCreated(
                settlement.Id,
                State.TripId,
                settlement.From,
                settlement.To,
                settlement.Amount,
                "USD",
                DateTime.UtcNow
            ), cancellationToken);
            
            State.SettlementsCreated.Add(settlement.Id);
        }
        
        State.SettlementsGenerated = true;

        // Send notifications to users about settlements
        foreach (var settlement in settlements)
        {
            await PublishAsync(new SendNotification(
                settlement.From,
                "settlement_due",
                "Settlement Required",
                $"You owe ${settlement.Amount:F2} to another trip member",
                new Dictionary<string, string>
                {
                    { "tripId", State.TripId },
                    { "settlementId", settlement.Id },
                    { "amount", settlement.Amount.ToString("F2") }
                }
            ), cancellationToken);
        }
        
        State.NotificationsSent = true;

        await CompleteAsync(cancellationToken);
    }

    protected override async Task OnCompensateAsync(CancellationToken cancellationToken)
    {
        // Compensation: Cancel created settlements
        await Task.CompletedTask;
    }

    private List<(string Id, string From, string To, decimal Amount)> CalculateSettlements(
        Dictionary<string, decimal> balances)
    {
        var settlements = new List<(string, string, string, decimal)>();
        
        // Split into debtors (negative balance) and creditors (positive balance)
        var debtors = balances.Where(b => b.Value < 0)
            .Select(b => (UserId: b.Key, Amount: -b.Value))
            .OrderByDescending(d => d.Amount)
            .ToList();
        
        var creditors = balances.Where(b => b.Value > 0)
            .Select(b => (UserId: b.Key, Amount: b.Value))
            .OrderByDescending(c => c.Amount)
            .ToList();

        // Greedy algorithm to minimize number of transactions
        int i = 0, j = 0;
        while (i < debtors.Count && j < creditors.Count)
        {
            var debtor = debtors[i];
            var creditor = creditors[j];
            
            var amount = Math.Min(debtor.Amount, creditor.Amount);
            
            settlements.Add((
                Guid.NewGuid().ToString(),
                debtor.UserId,
                creditor.UserId,
                Math.Round(amount, 2)
            ));
            
            debtors[i] = (debtor.UserId, debtor.Amount - amount);
            creditors[j] = (creditor.UserId, creditor.Amount - amount);
            
            if (debtors[i].Amount < 0.01m) i++;
            if (creditors[j].Amount < 0.01m) j++;
        }
        
        return settlements;
    }
}
