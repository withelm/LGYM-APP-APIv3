using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Training.Models;

public sealed class TrainingExerciseInput
{
    public string ExerciseId { get; init; } = string.Empty;
    public int Series { get; init; }
    public int Reps { get; init; }
    public double Weight { get; init; }
    public WeightUnits Unit { get; init; }
}
