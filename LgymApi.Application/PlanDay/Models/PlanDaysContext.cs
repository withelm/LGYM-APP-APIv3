using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;
using PlanDayExerciseEntity = LgymApi.Domain.Entities.PlanDayExercise;

namespace LgymApi.Application.Features.PlanDay.Models;

public sealed class PlanDaysContext
{
    public List<PlanDayEntity> PlanDays { get; init; } = new();
    public List<PlanDayExerciseEntity> PlanDayExercises { get; init; } = new();
    public Dictionary<Guid, ExerciseEntity> ExerciseMap { get; init; } = new();
}
