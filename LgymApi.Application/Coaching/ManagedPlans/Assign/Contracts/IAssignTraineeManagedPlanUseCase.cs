using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.ManagedPlans.Assign;

public interface IAssignTraineeManagedPlanUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(
        AssignTraineeManagedPlanCommand command,
        CancellationToken cancellationToken = default);
}
