namespace LgymApi.Domain.Entities;

public sealed class SupplementIntakeLog : EntityBase
{
    public Guid TraineeId { get; set; }
    public Guid PlanItemId { get; set; }
    public DateOnly IntakeDate { get; set; }
    public DateTimeOffset TakenAt { get; set; }

    public User Trainee { get; set; } = null!;
    public SupplementPlanItem PlanItem { get; set; } = null!;
}
