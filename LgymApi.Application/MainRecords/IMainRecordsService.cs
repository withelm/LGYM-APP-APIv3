using LgymApi.Application.Features.MainRecords.Models;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;

namespace LgymApi.Application.Features.MainRecords;

public interface IMainRecordsService
{
    Task AddNewRecordAsync(AddMainRecordInput input, CancellationToken cancellationToken = default);
    Task<List<MainRecordEntity>> GetMainRecordsHistoryAsync(Id<LgymApi.Domain.Entities.User> userId, CancellationToken cancellationToken = default);
    // Legacy name kept for backward compatibility: this returns the best (max) record per exercise.
    Task<MainRecordsLastContext> GetLastMainRecordsAsync(Id<LgymApi.Domain.Entities.User> userId, CancellationToken cancellationToken = default);
    Task DeleteMainRecordAsync(Id<LgymApi.Domain.Entities.User> currentUserId, Id<LgymApi.Domain.Entities.MainRecord> recordId, CancellationToken cancellationToken = default);
    Task UpdateMainRecordAsync(UpdateMainRecordInput input, CancellationToken cancellationToken = default);
    Task<PossibleRecordResult> GetRecordOrPossibleRecordInExerciseAsync(Id<LgymApi.Domain.Entities.User> userId, string exerciseId, CancellationToken cancellationToken = default);
}
