using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Plan.CheckIsUserHavePlan;

internal sealed class CheckIsUserHavePlanUseCase : ICheckIsUserHavePlanUseCase
{
    private readonly Func<CheckIsUserHavePlanQuery, CancellationToken, Task<Result<bool, AppError>>> _executeAsync;

    public CheckIsUserHavePlanUseCase(Func<CheckIsUserHavePlanQuery, CancellationToken, Task<Result<bool, AppError>>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
    }

    public Task<Result<bool, AppError>> ExecuteAsync(CheckIsUserHavePlanQuery input, CancellationToken cancellationToken = default)
        => _executeAsync(input, cancellationToken);
}
