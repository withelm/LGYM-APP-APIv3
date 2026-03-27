using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;

namespace LgymApi.Application.Features.Training.Models;

public sealed class TrainingByDateDetails
{
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training> Id { get; init; }
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.PlanDay> TypePlanDayId { get; init; }
    public DateTime CreatedAt { get; init; }
    public PlanDayEntity? PlanDay { get; init; }
    public string? Gym { get; init; }
    public List<EnrichedExercise> Exercises { get; init; } = new();
}
