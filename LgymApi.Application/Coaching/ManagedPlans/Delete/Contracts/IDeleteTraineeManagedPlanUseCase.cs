using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.ManagedPlans.Delete;

public interface IDeleteTraineeManagedPlanUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(
        DeleteTraineeManagedPlanCommand command,
        CancellationToken cancellationToken = default);
}
