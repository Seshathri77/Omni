using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniFlow.Messaging;
using Yath.Shared.DTOs;
using Yath.Shared.Messages;
using Yath.ExpenseService.Models;
using Yath.ExpenseService.Repositories;

namespace Yath.ExpenseService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IExpenseGroupRepository _groupRepository;
    private readonly ISettlementRepository _settlementRepository;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(
        IExpenseRepository expenseRepository,
        IExpenseGroupRepository groupRepository,
        ISettlementRepository settlementRepository,
        IMessageBus messageBus,
        ILogger<ExpensesController> logger)
    {
        _expenseRepository = expenseRepository;
        _groupRepository = groupRepository;
        _settlementRepository = settlementRepository;
        _messageBus = messageBus;
        _logger = logger;
    }

    [HttpPost("trip/{tripId}")]
    public async Task<ActionResult<ApiResponse<ExpenseDto>>> CreateExpense(string tripId, [FromBody] AddExpenseRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Validate splits
            var totalSplits = request.Splits.Sum(s => s.Amount);
            if (Math.Abs(totalSplits - request.Amount) > 0.01m)
                return BadRequest(new ApiResponse<ExpenseDto>(false, null, "Splits must equal total amount"));

            var expense = new Expense
            {
                ExpenseId = Guid.NewGuid().ToString(),
                TripId = tripId,
                PaidBy = userId,
                Amount = request.Amount,
                Currency = request.Currency,
                Category = Enum.Parse<ExpenseCategory>(request.Category, true),
                Description = request.Description,
                Splits = request.Splits.Select(s => new Models.ExpenseSplit
                {
                    UserId = s.UserId,
                    Amount = s.Amount,
                    Paid = s.UserId == userId
                }).ToList(),
                ReceiptUrl = request.ReceiptUrl,
                Date = request.Date
            };

            await _expenseRepository.CreateAsync(expense);

            // Update expense group balances
            var group = await _groupRepository.GetByTripIdAsync(tripId);
            if (group != null)
            {
                group.TotalExpenses += request.Amount;
                
                // Update balances: payer gets credit, others get debt
                foreach (var split in expense.Splits)
                {
                    if (!group.Balances.ContainsKey(split.UserId))
                        group.Balances[split.UserId] = 0;
                    
                    if (split.UserId == userId)
                    {
                        // Payer: credit = amount paid - their split
                        group.Balances[split.UserId] += expense.Amount - split.Amount;
                    }
                    else
                    {
                        // Others: debit = their split
                        group.Balances[split.UserId] -= split.Amount;
                    }
                }
                
                await _groupRepository.UpdateAsync(group);
            }

            // Publish event
            await _messageBus.PublishAsync(new ExpenseAdded(
                expense.ExpenseId,
                expense.TripId,
                expense.PaidBy,
                expense.Amount,
                expense.Currency,
                expense.Category.ToString().ToLower(),
                expense.Description,
                expense.Splits.Select(s => new Yath.Shared.Messages.ExpenseSplit(
                    s.UserId,
                    s.Amount,
                    s.Paid
                )).ToList(),
                expense.Date,
                DateTime.UtcNow
            ));

            _logger.LogInformation("Expense {ExpenseId} added to trip {TripId}", expense.ExpenseId, tripId);

            var expenseDto = MapToDto(expense);
            return Ok(new ApiResponse<ExpenseDto>(true, expenseDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return StatusCode(500, new ApiResponse<ExpenseDto>(false, null, "Failed to create expense"));
        }
    }

    [HttpGet("{expenseId}")]
    public async Task<ActionResult<ApiResponse<ExpenseDto>>> GetExpense(string expenseId)
    {
        try
        {
            var expense = await _expenseRepository.GetByIdAsync(expenseId);
            if (expense == null)
                return NotFound(new ApiResponse<ExpenseDto>(false, null, "Expense not found"));

            var expenseDto = MapToDto(expense);
            return Ok(new ApiResponse<ExpenseDto>(true, expenseDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching expense");
            return StatusCode(500, new ApiResponse<ExpenseDto>(false, null, "Failed to fetch expense"));
        }
    }

    [HttpGet("trip/{tripId}")]
    public async Task<ActionResult<ApiResponse<List<ExpenseDto>>>> GetTripExpenses(string tripId, [FromQuery] int skip = 0, [FromQuery] int limit = 50)
    {
        try
        {
            var expenses = await _expenseRepository.GetByTripIdAsync(tripId, skip, limit);
            var expenseDtos = expenses.Select(MapToDto).ToList();

            return Ok(new ApiResponse<List<ExpenseDto>>(true, expenseDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trip expenses");
            return StatusCode(500, new ApiResponse<List<ExpenseDto>>(false, null, "Failed to fetch expenses"));
        }
    }

    [HttpDelete("{expenseId}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteExpense(string expenseId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var expense = await _expenseRepository.GetByIdAsync(expenseId);
            if (expense == null)
                return NotFound(new ApiResponse<bool>(false, false, "Expense not found"));

            if (expense.PaidBy != userId)
                return Forbid();

            // Reverse balance changes
            var group = await _groupRepository.GetByTripIdAsync(expense.TripId);
            if (group != null)
            {
                group.TotalExpenses -= expense.Amount;
                
                foreach (var split in expense.Splits)
                {
                    if (split.UserId == userId)
                    {
                        group.Balances[split.UserId] -= expense.Amount - split.Amount;
                    }
                    else
                    {
                        group.Balances[split.UserId] += split.Amount;
                    }
                }
                
                await _groupRepository.UpdateAsync(group);
            }

            await _expenseRepository.DeleteAsync(expenseId);

            // Publish event
            await _messageBus.PublishAsync(new ExpenseDeleted(
                expenseId,
                expense.TripId,
                DateTime.UtcNow
            ));

            _logger.LogInformation("Expense {ExpenseId} deleted", expenseId);

            return Ok(new ApiResponse<bool>(true, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense");
            return StatusCode(500, new ApiResponse<bool>(false, false, "Failed to delete expense"));
        }
    }

    [HttpGet("trip/{tripId}/summary")]
    public async Task<ActionResult<ApiResponse<ExpenseSummaryDto>>> GetExpenseSummary(string tripId)
    {
        try
        {
            var group = await _groupRepository.GetByTripIdAsync(tripId);
            if (group == null)
                return NotFound(new ApiResponse<ExpenseSummaryDto>(false, null, "Expense group not found"));

            // Calculate who owes whom
            var balances = new List<BalanceDto>();
            var debtors = group.Balances.Where(b => b.Value < 0).ToList();
            var creditors = group.Balances.Where(b => b.Value > 0).ToList();
            
            foreach (var debtor in debtors)
            {
                foreach (var creditor in creditors)
                {
                    if (creditor.Value > 0.01m && debtor.Value < -0.01m)
                    {
                        balances.Add(new BalanceDto(
                            debtor.Key,
                            string.Empty, // FromUserName enriched by client
                            creditor.Key,
                            string.Empty, // ToUserName enriched by client
                            Math.Min(-debtor.Value, creditor.Value)
                        ));
                    }
                }
            }

            var summary = new ExpenseSummaryDto(
                tripId,
                group.TotalExpenses,
                group.Currency,
                group.Balances.ToDictionary(b => b.Key, b => b.Value),
                balances
            );

            return Ok(new ApiResponse<ExpenseSummaryDto>(true, summary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching expense summary");
            return StatusCode(500, new ApiResponse<ExpenseSummaryDto>(false, null, "Failed to fetch summary"));
        }
    }

    [HttpPost("trip/{tripId}/settlements")]
    public async Task<ActionResult<ApiResponse<List<SettlementDto>>>> GenerateSettlements(string tripId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var group = await _groupRepository.GetByTripIdAsync(tripId);
            if (group == null)
                return NotFound(new ApiResponse<List<SettlementDto>>(false, null, "Expense group not found"));

            // Calculate optimal settlements
            var settlements = CalculateSettlements(group.Balances, tripId);
            
            foreach (var settlement in settlements)
            {
                await _settlementRepository.CreateAsync(settlement);
                
                // Publish event
                await _messageBus.PublishAsync(new SettlementCreated(
                    settlement.SettlementId,
                    settlement.TripId,
                    settlement.From,
                    settlement.To,
                    settlement.Amount,
                    settlement.Currency,
                    DateTime.UtcNow
                ));
            }

            _logger.LogInformation("Generated {Count} settlements for trip {TripId}", settlements.Count, tripId);

            var settlementDtos = settlements.Select(s => new SettlementDto(
                s.SettlementId,
                s.TripId,
                s.From,
                string.Empty, // FromUserName enriched by client
                s.To,
                string.Empty, // ToUserName enriched by client
                s.Amount,
                s.Currency,
                s.Status.ToString().ToLower(),
                s.SettledAt,
                s.CreatedAt
            )).ToList();

            return Ok(new ApiResponse<List<SettlementDto>>(true, settlementDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating settlements");
            return StatusCode(500, new ApiResponse<List<SettlementDto>>(false, null, "Failed to generate settlements"));
        }
    }

    [HttpPost("settlements/{settlementId}/complete")]
    public async Task<ActionResult<ApiResponse<bool>>> CompleteSettlement(string settlementId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var settlement = await _settlementRepository.GetByIdAsync(settlementId);
            if (settlement == null)
                return NotFound(new ApiResponse<bool>(false, false, "Settlement not found"));

            // Only the creditor can mark as completed
            if (settlement.To != userId)
                return Forbid();

            settlement.Status = SettlementStatus.Completed;
            settlement.SettledAt = DateTime.UtcNow;
            
            await _settlementRepository.UpdateAsync(settlement);

            // Publish event
            await _messageBus.PublishAsync(new SettlementCompleted(
                settlementId,
                settlement.TripId,
                DateTime.UtcNow
            ));

            _logger.LogInformation("Settlement {SettlementId} completed", settlementId);

            return Ok(new ApiResponse<bool>(true, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing settlement");
            return StatusCode(500, new ApiResponse<bool>(false, false, "Failed to complete settlement"));
        }
    }

    [HttpGet("trip/{tripId}/settlements")]
    public async Task<ActionResult<ApiResponse<List<SettlementDto>>>> GetTripSettlements(string tripId)
    {
        try
        {
            var settlements = await _settlementRepository.GetByTripIdAsync(tripId);
            var settlementDtos = settlements.Select(s => new SettlementDto(
                s.SettlementId,
                s.TripId,
                s.From,
                string.Empty, // FromUserName enriched by client
                s.To,
                string.Empty, // ToUserName enriched by client
                s.Amount,
                s.Currency,
                s.Status.ToString().ToLower(),
                s.SettledAt,
                s.CreatedAt
            )).ToList();

            return Ok(new ApiResponse<List<SettlementDto>>(true, settlementDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching settlements");
            return StatusCode(500, new ApiResponse<List<SettlementDto>>(false, null, "Failed to fetch settlements"));
        }
    }

    private ExpenseDto MapToDto(Expense expense)
    {
        return new ExpenseDto(
            expense.ExpenseId,
            expense.TripId,
            expense.PaidBy,
            string.Empty, // PaidByName enriched by client
            expense.Amount,
            expense.Currency,
            expense.Category.ToString().ToLower(),
            expense.Description,
            expense.Splits.Select(s => new ExpenseSplitDto(
                s.UserId,
                string.Empty, // Username enriched by client
                string.Empty, // DisplayName enriched by client
                s.Amount,
                s.Paid
            )).ToList(),
            expense.ReceiptUrl,
            expense.Date,
            expense.CreatedAt
        );
    }

    private List<Settlement> CalculateSettlements(Dictionary<string, decimal> balances, string tripId)
    {
        var settlements = new List<Settlement>();
        
        var debtors = balances.Where(b => b.Value < 0)
            .Select(b => (UserId: b.Key, Amount: -b.Value))
            .OrderByDescending(d => d.Amount)
            .ToList();
        
        var creditors = balances.Where(b => b.Value > 0)
            .Select(b => (UserId: b.Key, Amount: b.Value))
            .OrderByDescending(c => c.Amount)
            .ToList();

        int i = 0, j = 0;
        while (i < debtors.Count && j < creditors.Count)
        {
            var debtor = debtors[i];
            var creditor = creditors[j];
            
            var amount = Math.Min(debtor.Amount, creditor.Amount);
            
            settlements.Add(new Settlement
            {
                SettlementId = Guid.NewGuid().ToString(),
                TripId = tripId,
                From = debtor.UserId,
                To = creditor.UserId,
                Amount = Math.Round(amount, 2),
                Currency = "USD",
                Status = SettlementStatus.Pending
            });
            
            debtors[i] = (debtor.UserId, debtor.Amount - amount);
            creditors[j] = (creditor.UserId, creditor.Amount - amount);
            
            if (debtors[i].Amount < 0.01m) i++;
            if (creditors[j].Amount < 0.01m) j++;
        }
        
        return settlements;
    }
}
