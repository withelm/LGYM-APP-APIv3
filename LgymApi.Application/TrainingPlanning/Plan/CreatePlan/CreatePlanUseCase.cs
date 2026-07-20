using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Plan.CreatePlan;

internal sealed class CreatePlanUseCase : ICreatePlanUseCase
{
    private readonly Func<CreatePlanCommand, CancellationToken, Task<Result<Unit, AppError>>> _executeAsync;

    public CreatePlanUseCase(Func<CreatePlanCommand, CancellationToken, Task<Result<Unit, AppError>>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
    }

    public Task<Result<Unit, AppError>> ExecuteAsync(CreatePlanCommand input, CancellationToken cancellationToken = default)
        => _executeAsync(input, cancellationToken);
}
