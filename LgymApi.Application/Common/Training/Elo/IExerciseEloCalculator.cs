using LgymApi.Domain.Enums;

namespace LgymApi.Application.Common.Training.Elo;

public interface IExerciseEloCalculator
{
    ExerciseEloFormula Formula { get; }

    int Calculate(ExerciseEloCalculationInput input);
}
