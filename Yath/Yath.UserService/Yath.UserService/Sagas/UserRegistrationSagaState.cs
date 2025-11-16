using OmniFlow.Sagas;

namespace Yath.UserService.Sagas;

public class UserRegistrationSagaState : SagaState
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool UserCreated { get; set; }
    public bool WelcomeEmailRequested { get; set; }
}
