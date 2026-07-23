using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.Progress.TrainingDates;

public interface IGetTrainingDatesUseCase
{
    Task<Result<List<DateTime>, AppError>> ExecuteAsync(
        GetTrainingDatesQuery query,
        CancellationToken cancellationToken = default);
}
