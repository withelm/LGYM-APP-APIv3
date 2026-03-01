namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Command dispatched when a trainer invitation is created.
/// Triggers invitation email sending to the trainee.
/// </summary>
public sealed class InvitationCreatedCommand : IActionCommand
{
    public Guid InvitationId { get; init; }
    public string InvitationCode { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public string TrainerName { get; init; } = string.Empty;
    public string RecipientEmail { get; init; } = string.Empty;
    public string CultureName { get; init; } = "en-US";
}
