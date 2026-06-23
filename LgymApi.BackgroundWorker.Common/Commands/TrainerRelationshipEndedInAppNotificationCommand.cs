using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Dispatched when a trainee detaches from an active trainer relationship.
/// Triggers an in-app notification for the trainer.
/// </summary>
public sealed class TrainerRelationshipEndedInAppNotificationCommand : IActionCommand
{
    public Id<User> TrainerId { get; init; }
    public Id<User> TraineeId { get; init; }
}
