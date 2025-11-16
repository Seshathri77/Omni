using OmniFlow.Sagas;

namespace Yath.ExpenseService.Sagas;

public class ExpenseSettlementSagaState : SagaState
{
    public string TripId { get; set; } = string.Empty;
    public Dictionary<string, decimal> Balances { get; set; } = new();
    public List<string> SettlementsCreated { get; set; } = new();
    public bool SettlementsGenerated { get; set; }
    public bool NotificationsSent { get; set; }
}
