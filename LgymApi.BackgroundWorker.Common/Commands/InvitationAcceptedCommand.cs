namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Command dispatched when a trainer invitation is accepted.
/// Contains minimal payload: only invitation identifier required for processing.
/// </summary>
public sealed class InvitationAcceptedCommand : IActionCommand
{
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation> InvitationId { get; init; }

}
