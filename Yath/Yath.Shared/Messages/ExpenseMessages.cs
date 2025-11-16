using OmniFlow.Core;

namespace Yath.Shared.Messages;

// ============================================================================
// EXPENSE COMMANDS
// ============================================================================

public record AddExpense(
    string TripId,
    string PaidBy,
    decimal Amount,
    string Currency,
    string Category, // "accommodation", "food", "transport", "activities", "other"
    string Description,
    List<ExpenseSplit> Splits,
    string? ReceiptUrl,
    DateTime Date
) : ICommand;

public record UpdateExpense(
    string ExpenseId,
    decimal? Amount,
    string? Description,
    List<ExpenseSplit>? Splits
) : ICommand;

public record DeleteExpense(
    string ExpenseId,
    string UserId
) : ICommand;

public record RecordSettlement(
    string TripId,
    string FromUserId,
    string ToUserId,
    decimal Amount,
    string Currency
) : ICommand;

// ============================================================================
// EXPENSE EVENTS
// ============================================================================

public record ExpenseAdded(
    string ExpenseId,
    string TripId,
    string PaidBy,
    decimal Amount,
    string Currency,
    string Category,
    string Description,
    List<ExpenseSplit> Splits,
    DateTime Date,
    DateTime CreatedAt
) : IEvent;

public record ExpenseUpdated(
    string ExpenseId,
    DateTime UpdatedAt
) : IEvent;

public record ExpenseDeleted(
    string ExpenseId,
    string TripId,
    DateTime DeletedAt
) : IEvent;

public record SettlementCreated(
    string SettlementId,
    string TripId,
    string FromUserId,
    string ToUserId,
    decimal Amount,
    string Currency,
    DateTime CreatedAt
) : IEvent;

public record SettlementCompleted(
    string SettlementId,
    string TripId,
    DateTime CompletedAt
) : IEvent;

public record SettlementRecorded(
    string SettlementId,
    string TripId,
    string FromUserId,
    string ToUserId,
    decimal Amount,
    string Currency,
    DateTime SettledAt
) : IEvent;

public record ExpenseBalancesUpdated(
    string TripId,
    Dictionary<string, decimal> Balances,
    DateTime UpdatedAt
) : IEvent;

// ============================================================================
// SUPPORTING TYPES
// ============================================================================

public record ExpenseSplit(
    string UserId,
    decimal Amount,
    bool IsPaid = false
);
