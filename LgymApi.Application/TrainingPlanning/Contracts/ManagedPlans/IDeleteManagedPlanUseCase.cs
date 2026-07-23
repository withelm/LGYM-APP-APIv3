using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;

public interface IDeleteManagedPlanUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(
        DeleteManagedPlanCommand command,
        CancellationToken cancellationToken = default);
}
