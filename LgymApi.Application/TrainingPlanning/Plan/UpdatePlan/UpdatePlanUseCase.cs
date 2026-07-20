using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Plan.UpdatePlan;

internal sealed class UpdatePlanUseCase : IUpdatePlanUseCase
{
    private readonly Func<UpdatePlanCommand, CancellationToken, Task<Result<Unit, AppError>>> _executeAsync;

    public UpdatePlanUseCase(Func<UpdatePlanCommand, CancellationToken, Task<Result<Unit, AppError>>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
    }

    public Task<Result<Unit, AppError>> ExecuteAsync(UpdatePlanCommand input, CancellationToken cancellationToken = default)
        => _executeAsync(input, cancellationToken);
}
