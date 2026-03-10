using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Training.Elo;

public sealed class AssistanceEloCalculationStrategy : IEloCalculationStrategy
{
    public EloStrategy Strategy => EloStrategy.Assistance;

    public int Calculate(ExerciseScore previousScore, ExerciseScore currentScore)
    {
        var prevWeight = previousScore.Weight.Value;
        var prevReps = previousScore.Reps;
        var currentWeight = currentScore.Weight.Value;
        var currentReps = currentScore.Reps;

        (prevWeight, currentWeight) = (currentWeight, prevWeight);
        (prevReps, currentReps) = (currentReps, prevReps);

        return CoreEloCalculator.Calculate(prevWeight, prevReps, currentWeight, currentReps);
    }
}
