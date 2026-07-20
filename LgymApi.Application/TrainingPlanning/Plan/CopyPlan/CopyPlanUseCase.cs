using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.TrainingPlanning.Plan.Models;

namespace LgymApi.Application.TrainingPlanning.Plan.CopyPlan;

internal sealed class CopyPlanUseCase : ICopyPlanUseCase
{
    private readonly Func<CopyPlanCommand, CancellationToken, Task<Result<PlanReadModel, AppError>>> _executeAsync;

    public CopyPlanUseCase(Func<CopyPlanCommand, CancellationToken, Task<Result<PlanReadModel, AppError>>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
    }

    public Task<Result<PlanReadModel, AppError>> ExecuteAsync(CopyPlanCommand input, CancellationToken cancellationToken = default)
        => _executeAsync(input, cancellationToken);
}
