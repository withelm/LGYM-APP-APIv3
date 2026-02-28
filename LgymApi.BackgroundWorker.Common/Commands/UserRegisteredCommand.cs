namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Typed command dispatched when a new user is registered in the system.
/// Encapsulates all data required by the welcome email handler.
/// </summary>
public sealed class UserRegisteredCommand : IActionCommand
{
    /// <summary>
    /// Gets the ID of the newly registered user.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Gets the user's display name.
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user's email address for welcome email notification.
    /// </summary>
    public string RecipientEmail { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user's preferred culture/language for email composition.
    /// Defaults to "en-US" if not provided or empty.
    /// </summary>
    public string CultureName { get; init; } = "en-US";
}
