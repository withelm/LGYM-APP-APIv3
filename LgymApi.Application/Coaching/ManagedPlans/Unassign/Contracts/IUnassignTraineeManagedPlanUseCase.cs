using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.ManagedPlans.Unassign;

public interface IUnassignTraineeManagedPlanUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(
        UnassignTraineeManagedPlanCommand command,
        CancellationToken cancellationToken = default);
}
