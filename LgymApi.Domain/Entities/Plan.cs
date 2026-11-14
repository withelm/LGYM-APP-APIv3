using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

public sealed class Plan : EntityBase<Plan>
{
    public ValueObjects.Id<User> UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public PlanPurpose Purpose { get; set; } = PlanPurpose.Unclassified;
    public bool IsActive { get; set; } = true;
    public string? ShareCode { get; set; }
    public DateTimeOffset? ShareCodeExpiresAt { get; set; }
    public int? ShareCodeDurationDays { get; set; }
    public long ShareCodeRevision { get; set; }

    public User? User { get; set; }
    public ICollection<PlanDay> PlanDays { get; set; } = new List<PlanDay>();
}
