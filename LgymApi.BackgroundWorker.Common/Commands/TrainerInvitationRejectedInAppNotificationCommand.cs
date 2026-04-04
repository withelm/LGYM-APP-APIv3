using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Dispatched when a trainee rejects a trainer invitation.
/// Triggers in-app notification for the trainer.
/// </summary>
public sealed class TrainerInvitationRejectedInAppNotificationCommand : IActionCommand
{
    public Id<User> TrainerId { get; init; }
    public Id<User> TraineeId { get; init; }
}
