using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Plan;

public sealed partial class PlanService
{
    public async Task<Result<PlanEntity, AppError>> CopyPlanAsync(UserEntity currentUser, string shareCode, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            return Result<PlanEntity, AppError>.Failure(new PlanUnauthorizedError(Messages.Unauthorized));
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var plan = await _planRepository.CopyPlanByShareCodeAsync(shareCode, currentUser.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PlanEntity, AppError>.Success(plan);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PlanEntity, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<Result<string, AppError>> GenerateShareCodeAsync(UserEntity currentUser, Id<PlanEntity> planId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planId.IsEmpty)
        {
            return Result<string, AppError>.Failure(new InvalidPlanError(Messages.InvalidId));
        }

        try
        {
            var shareCode = await _planRepository.GenerateShareCodeAsync(planId, currentUser.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<string, AppError>.Success(shareCode);
        }
        catch (KeyNotFoundException)
        {
            return Result<string, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind));
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
