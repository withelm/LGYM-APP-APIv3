using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;

public interface IGetManagedPlansUseCase
{
    Task<Result<IReadOnlyList<ManagedPlanReadModel>, AppError>> ExecuteAsync(
        GetManagedPlansQuery query,
        CancellationToken cancellationToken = default);
}
