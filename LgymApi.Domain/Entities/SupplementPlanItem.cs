namespace LgymApi.Domain.Entities;

public sealed class SupplementPlanItem : EntityBase
{
    public Guid PlanId { get; set; }
    public string SupplementName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public int DaysOfWeekMask { get; set; } = 127;
    public TimeSpan TimeOfDay { get; set; }
    public int Order { get; set; }

    public SupplementPlan Plan { get; set; } = null!;
}
