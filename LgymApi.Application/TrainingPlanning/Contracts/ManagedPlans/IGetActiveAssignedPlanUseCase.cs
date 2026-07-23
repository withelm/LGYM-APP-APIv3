using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;

public interface IGetActiveAssignedPlanUseCase
{
    Task<Result<ManagedPlanReadModel, AppError>> ExecuteAsync(
        GetActiveAssignedPlanQuery query,
        CancellationToken cancellationToken = default);
}
