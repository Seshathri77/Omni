using OmniFlow.Core;

namespace PaymentsService.Messages;

// Commands
public record ProcessPayment(string OrderId, decimal Amount) : ICommand;
