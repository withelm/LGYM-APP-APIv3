namespace LgymApi.Domain.Entities;

public sealed class PlanDayExercise : EntityBase
{
    public Guid PlanDayId { get; set; }
    public Guid ExerciseId { get; set; }
    public int Series { get; set; }
    public string Reps { get; set; } = string.Empty;

    public PlanDay? PlanDay { get; set; }
    public Exercise? Exercise { get; set; }
}
