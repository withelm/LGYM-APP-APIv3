using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class SupplementIntakeLog : EntityBase<SupplementIntakeLog>
{
    public Id<User> TraineeId { get; set; }
    public Id<SupplementPlanItem> PlanItemId { get; set; }
    public DateOnly IntakeDate { get; set; }
    public DateTimeOffset TakenAt { get; set; }

    public User Trainee { get; set; } = null!;
    public SupplementPlanItem PlanItem { get; set; } = null!;
}
