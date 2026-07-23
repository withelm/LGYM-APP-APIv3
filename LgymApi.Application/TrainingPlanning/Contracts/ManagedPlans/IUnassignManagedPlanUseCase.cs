using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;

public interface IUnassignManagedPlanUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(
        UnassignManagedPlanCommand command,
        CancellationToken cancellationToken = default);
}
