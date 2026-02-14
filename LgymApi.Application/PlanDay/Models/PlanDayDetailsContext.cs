using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;
using PlanDayExerciseEntity = LgymApi.Domain.Entities.PlanDayExercise;

namespace LgymApi.Application.Features.PlanDay.Models;

public sealed class PlanDayDetailsContext
{
    public PlanDayEntity PlanDay { get; init; } = null!;
    public List<PlanDayExerciseEntity> Exercises { get; init; } = new();
    public Dictionary<Guid, ExerciseEntity> ExerciseMap { get; init; } = new();
}
