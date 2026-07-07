namespace LgymApi.Application.Common.Training.Elo;

public sealed record ExerciseEloCalculationInput(
    double PreviousWeight,
    double PreviousReps,
    double CurrentWeight,
    double CurrentReps);
