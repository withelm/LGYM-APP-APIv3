using LgymApi.Application.Features.MainRecords.Models;
using LgymApi.Domain.Enums;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;

namespace LgymApi.Application.Features.MainRecords;

public interface IMainRecordsService
{
    Task AddNewRecordAsync(Guid userId, string exerciseId, double weight, WeightUnits unit, DateTime date, CancellationToken cancellationToken = default);
    Task<List<MainRecordEntity>> GetMainRecordsHistoryAsync(Guid userId, CancellationToken cancellationToken = default);
    // Legacy name kept for backward compatibility: this returns the best (max) record per exercise.
    Task<MainRecordsLastContext> GetLastMainRecordsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeleteMainRecordAsync(Guid recordId, CancellationToken cancellationToken = default);
    Task UpdateMainRecordAsync(Guid userId, string recordId, string exerciseId, double weight, WeightUnits unit, DateTime date, CancellationToken cancellationToken = default);
    Task<PossibleRecordResult> GetRecordOrPossibleRecordInExerciseAsync(Guid userId, string exerciseId, CancellationToken cancellationToken = default);
}
