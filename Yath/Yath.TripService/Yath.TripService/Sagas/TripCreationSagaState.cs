using OmniFlow.Sagas;

namespace Yath.TripService.Sagas;

public class TripCreationSagaState : SagaState
{
    public string TripId { get; set; } = string.Empty;
    public string CreatorId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> ParticipantIds { get; set; } = new();
    public bool TripCreated { get; set; }
    public bool ChatRoomRequested { get; set; }
    public bool ExpenseGroupRequested { get; set; }
    public bool NotificationsSent { get; set; }
}
