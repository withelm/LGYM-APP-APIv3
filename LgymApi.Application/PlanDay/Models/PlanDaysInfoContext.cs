using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;
using PlanDayExerciseEntity = LgymApi.Domain.Entities.PlanDayExercise;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.PlanDay.Models;

public sealed class PlanDaysInfoContext
{
    public List<PlanDayEntity> PlanDays { get; init; } = new();
    public List<PlanDayExerciseEntity> PlanDayExercises { get; init; } = new();
    public Dictionary<Id<PlanDayEntity>, DateTime?> LastTrainingMap { get; init; } = new();
}
