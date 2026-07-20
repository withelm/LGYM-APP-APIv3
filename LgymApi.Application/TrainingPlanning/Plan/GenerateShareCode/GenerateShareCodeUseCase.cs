using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Plan.GenerateShareCode;

internal sealed class GenerateShareCodeUseCase : IGenerateShareCodeUseCase
{
    private readonly Func<GenerateShareCodeCommand, CancellationToken, Task<Result<string, AppError>>> _executeAsync;

    public GenerateShareCodeUseCase(Func<GenerateShareCodeCommand, CancellationToken, Task<Result<string, AppError>>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
    }

    public Task<Result<string, AppError>> ExecuteAsync(GenerateShareCodeCommand input, CancellationToken cancellationToken = default)
        => _executeAsync(input, cancellationToken);
}
