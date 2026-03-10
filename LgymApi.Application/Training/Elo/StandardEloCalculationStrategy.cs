using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Training.Elo;

public sealed class StandardEloCalculationStrategy : IEloCalculationStrategy
{
    public EloStrategy Strategy => EloStrategy.Standard;

    public int Calculate(ExerciseScore previousScore, ExerciseScore currentScore)
    {
        return CoreEloCalculator.Calculate(
            previousScore.Weight.Value,
            previousScore.Reps,
            currentScore.Weight.Value,
            currentScore.Reps);
    }
}
