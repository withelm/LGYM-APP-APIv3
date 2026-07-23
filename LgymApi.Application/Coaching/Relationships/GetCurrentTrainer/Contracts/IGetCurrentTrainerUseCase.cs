using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.Relationships.GetCurrentTrainer;

public interface IGetCurrentTrainerUseCase
{
    Task<Result<CurrentTrainerReadModel, AppError>> ExecuteAsync(
        GetCurrentTrainerQuery query,
        CancellationToken cancellationToken = default);
}
