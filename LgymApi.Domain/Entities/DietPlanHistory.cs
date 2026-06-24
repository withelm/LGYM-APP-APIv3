using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class DietPlanHistory : EntityBase<DietPlanHistory>
{
    public Id<DietPlan> DietPlanId { get; set; }
    public Id<User> ChangedByUserId { get; set; }
    public DateTimeOffset ChangeDate { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;

    public DietPlan DietPlan { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}
