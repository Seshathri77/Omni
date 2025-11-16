using OmniFlow.Sagas;
using Yath.Shared.Messages;

namespace Yath.UserService.Sagas;

public class UserRegistrationSaga : Saga<UserRegistrationSagaState>
{
    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        // Step 1: Mark user as created (already done before saga starts)
        State.UserCreated = true;

        // Step 2: Request welcome email via notification service
        await PublishAsync(new WelcomeEmailRequested(
            State.UserId,
            State.Email,
            State.DisplayName
        ), cancellationToken);

        State.WelcomeEmailRequested = true;

        // Step 3: Publish UserRegistered event for other services
        await PublishAsync(new UserRegistered(
            State.UserId,
            State.Username,
            State.Email,
            State.DisplayName,
            DateTime.UtcNow
        ), cancellationToken);

        // Complete the saga
        await CompleteAsync(cancellationToken);
    }

    protected override async Task OnCompensateAsync(CancellationToken cancellationToken)
    {
        // Compensation: If saga fails, we would need to delete the user
        // (Implementation would involve calling user repository to delete)
        await Task.CompletedTask;
    }
}
