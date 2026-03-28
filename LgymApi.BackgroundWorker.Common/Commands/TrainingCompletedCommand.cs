
namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Command dispatched when a training session is completed.
/// Contains minimal payload: only identifiers required for processing.
/// </summary>
public sealed class TrainingCompletedCommand : IActionCommand
{
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User> UserId { get; init; }
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training> TrainingId { get; init; }

}
