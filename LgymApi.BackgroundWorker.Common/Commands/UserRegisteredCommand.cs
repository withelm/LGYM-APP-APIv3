namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Command dispatched when a user completes registration.
/// Contains minimal payload: only user identifier required for processing.
/// </summary>
public sealed class UserRegisteredCommand : IActionCommand
{
    public Guid UserId { get; init; }

}
