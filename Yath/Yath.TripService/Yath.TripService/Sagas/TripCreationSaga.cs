using OmniFlow.Sagas;
using Yath.Shared.Messages;

namespace Yath.TripService.Sagas;

public class TripCreationSaga : Saga<TripCreationSagaState>
{
    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        // Step 1: Trip is already created (done before saga starts)
        State.TripCreated = true;

        // Step 2: Request chat room creation
        await PublishAsync(new CreateChatRoom(
            State.TripId,
            State.ParticipantIds
        ), cancellationToken);
        State.ChatRoomRequested = true;

        // Step 3: Initialize expense group (if participants exist)
        if (State.ParticipantIds.Count > 1)
        {
            // Expense service will listen to TripCreated event
            State.ExpenseGroupRequested = true;
        }

        // Step 4: Send notifications to participants
        foreach (var participantId in State.ParticipantIds.Where(p => p != State.CreatorId))
        {
            await PublishAsync(new SendNotification(
                participantId,
                "trip_invite",
                "Trip Invitation",
                $"You've been added to {State.Title}",
                new Dictionary<string, string>
                {
                    { "tripId", State.TripId },
                    { "creatorId", State.CreatorId }
                }
            ), cancellationToken);
        }
        State.NotificationsSent = true;

        // Complete the saga
        await CompleteAsync(cancellationToken);
    }

    protected override async Task OnCompensateAsync(CancellationToken cancellationToken)
    {
        // Compensation: Would need to delete trip and clean up
        await Task.CompletedTask;
    }
}
