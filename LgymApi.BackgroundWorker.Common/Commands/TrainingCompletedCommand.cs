
namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Command dispatched when a training session is completed.
/// Contains minimal payload: only identifiers required for processing.
/// </summary>
public sealed class TrainingCompletedCommand : IActionCommand
{
    public Guid UserId { get; init; }
    public Guid TrainingId { get; init; }

}
