using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Plan.SetActivePlan;

internal sealed class SetActivePlanUseCase : ISetActivePlanUseCase
{
    private readonly Func<SetActivePlanCommand, CancellationToken, Task<Result<Unit, AppError>>> _executeAsync;

    public SetActivePlanUseCase(Func<SetActivePlanCommand, CancellationToken, Task<Result<Unit, AppError>>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
    }

    public Task<Result<Unit, AppError>> ExecuteAsync(SetActivePlanCommand input, CancellationToken cancellationToken = default)
        => _executeAsync(input, cancellationToken);
}
