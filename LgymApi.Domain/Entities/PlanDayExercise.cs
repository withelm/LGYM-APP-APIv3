namespace LgymApi.Domain.Entities;

public sealed class PlanDayExercise : EntityBase<PlanDayExercise>
{
    public ValueObjects.Id<PlanDay> PlanDayId { get; set; }
    public ValueObjects.Id<Exercise> ExerciseId { get; set; }
    public int Order { get; set; }
    public int Series { get; set; }
    public string Reps { get; set; } = string.Empty;

    public PlanDay? PlanDay { get; set; }
    public Exercise? Exercise { get; set; }
}
