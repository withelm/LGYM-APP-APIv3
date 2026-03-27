using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Training.Models;

public sealed record AddTrainingInput(
    Id<LgymApi.Domain.Entities.Gym> GymId,
    Id<LgymApi.Domain.Entities.PlanDay> PlanDayId,
    DateTime CreatedAt,
    IReadOnlyCollection<TrainingExerciseInput> Exercises);
