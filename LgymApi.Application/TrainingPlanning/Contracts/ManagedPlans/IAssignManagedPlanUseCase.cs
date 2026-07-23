using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;

public interface IAssignManagedPlanUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(
        AssignManagedPlanCommand command,
        CancellationToken cancellationToken = default);
}
