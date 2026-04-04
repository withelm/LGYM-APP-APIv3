using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Dispatched when a trainer creates an invitation for a trainee.
/// Triggers in-app notification for the trainee.
/// </summary>
public sealed class TrainerInvitationCreatedInAppNotificationCommand : IActionCommand
{
    public Id<User> TraineeId { get; init; }
    public Id<User> TrainerId { get; init; }
}
