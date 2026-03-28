using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.MainRecords.Models;

public sealed class MainRecordsLastContext
{
    public List<MainRecordEntity> Records { get; init; } = new();
    public Dictionary<Id<ExerciseEntity>, ExerciseEntity> ExerciseMap { get; init; } = new();
}
