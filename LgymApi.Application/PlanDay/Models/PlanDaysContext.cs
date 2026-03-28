using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;
using PlanDayExerciseEntity = LgymApi.Domain.Entities.PlanDayExercise;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.PlanDay.Models;

public sealed class PlanDaysContext
{
    public List<PlanDayEntity> PlanDays { get; init; } = new();
    public List<PlanDayExerciseEntity> PlanDayExercises { get; init; } = new();
    public Dictionary<Id<ExerciseEntity>, ExerciseEntity> ExerciseMap { get; init; } = new();
    public Dictionary<Id<ExerciseEntity>, string> Translations { get; init; } = new();
}
