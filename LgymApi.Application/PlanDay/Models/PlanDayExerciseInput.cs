namespace LgymApi.Application.Features.PlanDay.Models;

public sealed class PlanDayExerciseInput
{
    public string ExerciseId { get; init; } = string.Empty;
    public int Series { get; init; }
    public string Reps { get; init; } = string.Empty;
}
