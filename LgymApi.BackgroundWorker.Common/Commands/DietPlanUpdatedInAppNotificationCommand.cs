using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common.Commands;

public sealed class DietPlanUpdatedInAppNotificationCommand : IActionCommand
{
    public Id<DietPlan> DietPlanId { get; init; }
    public Id<User> TraineeId { get; init; }
    public Id<User> TrainerId { get; init; }
    public string DietPlanName { get; init; } = string.Empty;
    public DateTimeOffset TriggeredAt { get; init; }
}
