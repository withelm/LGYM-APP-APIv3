using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.TrainingPlanning.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Repositories;
using LgymApi.Resources;
using LgymApi.TrainingPlanning.Persistence;

namespace LgymApi.Application.TrainingPlanning.Plan.GenerateShareCode;

internal sealed class GenerateShareCodeUseCase : IGenerateShareCodeUseCase
{
    private readonly IPlanRepository _planRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public GenerateShareCodeUseCase(IPlanRepository planRepository, IUnitOfWork unitOfWork, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(planRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _planRepository = planRepository;
        _unitOfWork = unitOfWork;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Result<string, AppError>> ExecuteAsync(GenerateShareCodeCommand input, CancellationToken cancellationToken = default)
    {
        if (input.CurrentUserId.IsEmpty || input.PlanId.IsEmpty)
        {
            return Result<string, AppError>.Failure(new InvalidPlanError(Messages.InvalidId));
        }

        if (input.DurationDays is not (1 or 7 or 30))
        {
            return Result<string, AppError>.Failure(new InvalidPlanError(Messages.InvalidShareDuration));
        }

        try
        {
            var shareCode = await _planRepository.GenerateShareCodeAsync(input.PlanId, input.CurrentUserId, input.DurationDays, _clock.GetUtcNow(), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<string, AppError>.Success(shareCode);
        }
        catch (KeyNotFoundException)
        {
            return Result<string, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind));
        }
        catch (Exception exception) when (PlanShareConcurrency.IsConflict(exception, input.PlanId))
        {
            return Result<string, AppError>.Failure(new ConflictError(Messages.TryAgain));
        }
        catch (Exception exception) when (exception is InvalidOperationException || exception.GetType().Name == "DbUpdateException")
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return Result<string, AppError>.Failure(new PlanForbiddenError(Messages.Forbidden));
        }
    }
}
