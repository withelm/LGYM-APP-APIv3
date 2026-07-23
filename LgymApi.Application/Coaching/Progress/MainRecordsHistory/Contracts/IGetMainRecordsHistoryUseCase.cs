using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.WorkoutProgress.ProgressData.Models;

namespace LgymApi.Application.Coaching.Progress.MainRecordsHistory;

public interface IGetMainRecordsHistoryUseCase
{
    Task<Result<List<MainRecordReadModel>, AppError>> ExecuteAsync(
        GetMainRecordsHistoryQuery query,
        CancellationToken cancellationToken = default);
}
