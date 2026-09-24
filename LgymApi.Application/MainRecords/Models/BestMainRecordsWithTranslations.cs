using LgymApi.Application.WorkoutProgress.ProgressData.Models;
using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.MainRecords.Models;

public sealed class BestMainRecordsWithTranslations
{
    public List<MainRecordBestReadModel> Records { get; init; } = new();
    public IReadOnlyDictionary<Id<ExerciseEntity>, string> Translations { get; init; } = new Dictionary<Id<ExerciseEntity>, string>();
}
