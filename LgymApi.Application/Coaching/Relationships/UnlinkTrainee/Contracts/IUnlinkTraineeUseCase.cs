using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.Relationships.UnlinkTrainee;

public interface IUnlinkTraineeUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(UnlinkTraineeCommand command, CancellationToken cancellationToken = default);
}
