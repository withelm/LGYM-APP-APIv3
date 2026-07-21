using LgymApi.Application.Features.Training.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.WorkoutProgress.TrainingExecution;

public sealed record CompleteTrainingInput(
    Id<Gym> GymId,
    Id<PlanDay> PlanDayId,
    DateTime CreatedAt,
    IReadOnlyCollection<TrainingExerciseInput> Exercises);
