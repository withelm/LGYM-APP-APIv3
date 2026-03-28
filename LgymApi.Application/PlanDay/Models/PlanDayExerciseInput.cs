using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.PlanDay.Models;

public sealed class PlanDayExerciseInput
{
    public Id<LgymApi.Domain.Entities.Exercise> ExerciseId { get; init; }
    public int Series { get; init; }
    public string Reps { get; init; } = string.Empty;
}
