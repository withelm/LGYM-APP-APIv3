using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Training.Models;

public sealed class TrainingExerciseInput
{
    public Id<LgymApi.Domain.Entities.Exercise> ExerciseId { get; init; }
    public int Series { get; init; }
    public double Reps { get; init; }
    public double Weight { get; init; }
    public WeightUnits Unit { get; init; }
}
