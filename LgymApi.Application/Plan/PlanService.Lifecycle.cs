using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Plan;

public sealed partial class PlanService
{
    public async Task<Result<bool, AppError>> CheckIsUserHavePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId.IsEmpty)
        {
            return Result<bool, AppError>.Failure(new PlanFlagBadRequestError(Messages.InvalidId, false));
        }

        if (currentUser.Id != routeUserId)
        {
            return Result<bool, AppError>.Failure(new PlanFlagForbiddenError(Messages.Forbidden, false));
        }

        var plan = await _planRepository.FindActiveByUserIdAsync(currentUser.Id, cancellationToken);
        if (plan == null)
        {
            return Result<bool, AppError>.Success(false);
        }

        var planDayExists = await _planDayRepository.AnyByPlanIdAsync(plan.Id, cancellationToken);
        return Result<bool, AppError>.Success(planDayExists);
    }

    public async Task<Result<Unit, AppError>> SetNewActivePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, Id<PlanEntity> planId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId.IsEmpty || planId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidPlanError(Messages.InvalidId));
        }

        if (currentUser.Id != routeUserId)
        {
            return Result<Unit, AppError>.Failure(new PlanForbiddenError(Messages.Forbidden));
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null)
        {
            return Result<Unit, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind));
        }

        if (plan.UserId != currentUser.Id)
        {
            return Result<Unit, AppError>.Failure(new PlanForbiddenError(Messages.Forbidden));
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _planRepository.SetActivePlanAsync(currentUser.Id, planId, cancellationToken);
            currentUser.PlanId = planId;
            await _userRepository.UpdateAsync(currentUser, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<Unit, AppError>.Success(Unit.Value);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private sealed class PlanFlagBadRequestError : BadRequestError
    {
        private readonly object? _payload;

        public PlanFlagBadRequestError(string message, object? payload)
            : base(message)
        {
            _payload = payload;
        }

        public override object? GetPayload() => _payload;
    }

    private sealed class PlanFlagForbiddenError : ForbiddenError
    {
        private readonly object? _payload;

        public PlanFlagForbiddenError(string message, object? payload)
            : base(message)
        {
            _payload = payload;
        }

        public override object? GetPayload() => _payload;
    }
}
