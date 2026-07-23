using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;

namespace LgymApi.Application.Coaching.ManagedPlans.List;

public interface IListManagedPlansUseCase
{
    Task<Result<IReadOnlyList<ManagedPlanReadModel>, AppError>> ExecuteAsync(
        ListManagedPlansQuery query,
        CancellationToken cancellationToken = default);
}
