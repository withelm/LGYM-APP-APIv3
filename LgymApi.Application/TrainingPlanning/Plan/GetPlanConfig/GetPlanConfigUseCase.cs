using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.TrainingPlanning.Plan.Models;

namespace LgymApi.Application.TrainingPlanning.Plan.GetPlanConfig;

internal sealed class GetPlanConfigUseCase : IGetPlanConfigUseCase
{
    private readonly Func<GetPlanConfigQuery, CancellationToken, Task<Result<PlanReadModel, AppError>>> _executeAsync;

    public GetPlanConfigUseCase(Func<GetPlanConfigQuery, CancellationToken, Task<Result<PlanReadModel, AppError>>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
    }

    public Task<Result<PlanReadModel, AppError>> ExecuteAsync(GetPlanConfigQuery input, CancellationToken cancellationToken = default)
        => _executeAsync(input, cancellationToken);
}
