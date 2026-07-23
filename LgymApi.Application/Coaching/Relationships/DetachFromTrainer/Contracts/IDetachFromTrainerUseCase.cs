using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.Relationships.DetachFromTrainer;

public interface IDetachFromTrainerUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(DetachFromTrainerCommand command, CancellationToken cancellationToken = default);
}
