namespace LgymApi.Domain.Entities;

public sealed class PlanDay : EntityBase<PlanDay>
{
    public ValueObjects.Id<Plan> PlanId { get; set; }
    public string Name { get; set; } = string.Empty;

    public Plan? Plan { get; set; }
    public ICollection<PlanDayExercise> Exercises { get; set; } = new List<PlanDayExercise>();
}
