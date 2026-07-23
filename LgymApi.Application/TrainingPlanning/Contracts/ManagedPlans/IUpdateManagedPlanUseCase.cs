using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;

public interface IUpdateManagedPlanUseCase
{
    Task<Result<ManagedPlanReadModel, AppError>> ExecuteAsync(
        UpdateManagedPlanCommand command,
        CancellationToken cancellationToken = default);
}
