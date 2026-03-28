using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class SupplementPlanItem : EntityBase<SupplementPlanItem>
{
    public const DaysOfWeekSet EveryDayMask = DaysOfWeekSet.EveryDay;

    public Id<SupplementPlan> PlanId { get; set; }
    public string SupplementName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public DaysOfWeekSet DaysOfWeekMask { get; set; } = EveryDayMask;
    public TimeSpan TimeOfDay { get; set; }
    public int Order { get; set; }

    public SupplementPlan Plan { get; set; } = null!;
}
