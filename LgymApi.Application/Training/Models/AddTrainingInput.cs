namespace LgymApi.Application.Features.Training.Models;

public sealed record AddTrainingInput(
    Guid GymId,
    Guid PlanDayId,
    DateTime CreatedAt,
    IReadOnlyCollection<TrainingExerciseInput> Exercises);
