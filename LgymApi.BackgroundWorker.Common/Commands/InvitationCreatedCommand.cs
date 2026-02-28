namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Typed command dispatched when a trainer invitation is created.
/// Encapsulates all data required by the invitation email handler.
/// </summary>
public sealed class InvitationCreatedCommand : IActionCommand
{
    /// <summary>
    /// Gets the ID of the trainer invitation.
    /// </summary>
    public Guid InvitationId { get; init; }

    /// <summary>
    /// Gets the invitation code to be included in the email link.
    /// </summary>
    public string InvitationCode { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UTC date/time when the invitation expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the name of the trainer sending the invitation.
    /// </summary>
    public string TrainerName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the email address of the trainee receiving the invitation.
    /// </summary>
    public string RecipientEmail { get; init; } = string.Empty;

    /// <summary>
    /// Gets the trainee's (or trainer's) preferred culture/language for email composition.
    /// Defaults to "en-US" if not provided or empty.
    /// </summary>
    public string CultureName { get; init; } = "en-US";
}
