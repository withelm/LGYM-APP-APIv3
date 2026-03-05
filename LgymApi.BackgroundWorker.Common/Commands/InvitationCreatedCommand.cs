namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Command dispatched when a trainer invitation is created.
/// Contains minimal payload: only invitation identifier required for processing.
/// </summary>
public sealed class InvitationCreatedCommand : IActionCommand
{
    public Guid InvitationId { get; init; }

}
