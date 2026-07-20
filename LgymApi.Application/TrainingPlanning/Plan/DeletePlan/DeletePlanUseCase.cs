using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Plan.DeletePlan;

internal sealed class DeletePlanUseCase : IDeletePlanUseCase
{
    private readonly Func<DeletePlanCommand, CancellationToken, Task<Result<Unit, AppError>>> _executeAsync;

    public DeletePlanUseCase(Func<DeletePlanCommand, CancellationToken, Task<Result<Unit, AppError>>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
    }

    public Task<Result<Unit, AppError>> ExecuteAsync(DeletePlanCommand input, CancellationToken cancellationToken = default)
        => _executeAsync(input, cancellationToken);
}
