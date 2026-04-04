using LgymApi.Application.Features.MainRecords.Models;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;

namespace LgymApi.Application.Features.MainRecords;

public interface IMainRecordsService
{
    Task<Result<Unit, AppError>> AddNewRecordAsync(AddMainRecordInput input, CancellationToken cancellationToken = default);
    Task<Result<List<MainRecordEntity>, AppError>> GetMainRecordsHistoryAsync(Id<LgymApi.Domain.Entities.User> userId, CancellationToken cancellationToken = default);
    // Legacy name kept for backward compatibility: this returns the best (max) record per exercise.
    Task<Result<MainRecordsLastContext, AppError>> GetLastMainRecordsAsync(Id<LgymApi.Domain.Entities.User> userId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> DeleteMainRecordAsync(Id<LgymApi.Domain.Entities.User> currentUserId, Id<LgymApi.Domain.Entities.MainRecord> recordId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> UpdateMainRecordAsync(UpdateMainRecordInput input, CancellationToken cancellationToken = default);
    Task<Result<PossibleRecordResult, AppError>> GetRecordOrPossibleRecordInExerciseAsync(Id<LgymApi.Domain.Entities.User> userId, Id<LgymApi.Domain.Entities.Exercise> exerciseId, CancellationToken cancellationToken = default);
}
