using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.WorkoutProgress.Dashboard.Models;

public sealed class WorkoutProgressDashboardTrainingsWithTranslations
{
    public List<WorkoutProgressDashboardTrainingReadModel> Trainings { get; init; } = new();
    public IReadOnlyDictionary<Id<ExerciseEntity>, string> Translations { get; init; } = new Dictionary<Id<ExerciseEntity>, string>();
}
