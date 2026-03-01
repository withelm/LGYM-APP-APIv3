namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Command dispatched when a user completes registration.
/// Triggers welcome email sending to the new user.
/// </summary>
public sealed class UserRegisteredCommand : IActionCommand
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string RecipientEmail { get; init; } = string.Empty;
    public string CultureName { get; init; } = "en-US";
}
