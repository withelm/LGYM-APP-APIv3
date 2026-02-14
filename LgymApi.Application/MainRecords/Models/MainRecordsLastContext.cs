using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;

namespace LgymApi.Application.Features.MainRecords.Models;

public sealed class MainRecordsLastContext
{
    public List<MainRecordEntity> Records { get; init; } = new();
    public Dictionary<Guid, ExerciseEntity> ExerciseMap { get; init; } = new();
}
