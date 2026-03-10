using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Training.Models;

public sealed class ScoreResult
{
    public double Reps { get; init; }
    public double Weight { get; init; }
    public WeightUnits Unit { get; init; }
}
