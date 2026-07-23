using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;

namespace LgymApi.Application.Coaching.ManagedPlans.GetActive;

public interface IGetActiveManagedPlanUseCase
{
    Task<Result<ManagedPlanReadModel, AppError>> ExecuteAsync(
        GetActiveManagedPlanQuery query,
        CancellationToken cancellationToken = default);
}
