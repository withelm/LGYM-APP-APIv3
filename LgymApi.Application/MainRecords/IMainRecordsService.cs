using LgymApi.Application.Features.MainRecords.Models;
using LgymApi.Domain.Enums;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;

namespace LgymApi.Application.Features.MainRecords;

public interface IMainRecordsService
{
    Task AddNewRecordAsync(Guid userId, string exerciseId, double weight, WeightUnits unit, DateTime date);
    Task<List<MainRecordEntity>> GetMainRecordsHistoryAsync(Guid userId);
    // Legacy name kept for backward compatibility: this returns the best (max) record per exercise.
    Task<MainRecordsLastContext> GetLastMainRecordsAsync(Guid userId);
    Task DeleteMainRecordAsync(Guid recordId);
    Task UpdateMainRecordAsync(Guid userId, string recordId, string exerciseId, double weight, WeightUnits unit, DateTime date);
    Task<PossibleRecordResult> GetRecordOrPossibleRecordInExerciseAsync(Guid userId, string exerciseId);
}
