using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Training.Elo;

public interface IEloCalculationStrategy
{
    EloStrategy Strategy { get; }
    int Calculate(ExerciseScore previousScore, ExerciseScore currentScore);
}
