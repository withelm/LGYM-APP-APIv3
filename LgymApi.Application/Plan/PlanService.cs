using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Repositories;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Plan;

public sealed class PlanService : IPlanService
{
    private readonly IUserRepository _userRepository;
    private readonly IPlanRepository _planRepository;
    private readonly IPlanDayRepository _planDayRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PlanService(IUserRepository userRepository, IPlanRepository planRepository, IPlanDayRepository planDayRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _planRepository = planRepository;
        _planDayRepository = planDayRepository;
        _unitOfWork = unitOfWork;
    }

     public async Task<Result<Unit, AppError>> CreatePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, string name, CancellationToken cancellationToken = default)
     {
         if (currentUser == null || routeUserId.IsEmpty)
         {
              return Result<Unit, AppError>.Failure(new InvalidPlanError(Messages.InvalidId));
         }

        if (currentUser.Id != routeUserId)
        {
            return Result<Unit, AppError>.Failure(new PlanForbiddenError(Messages.Forbidden));
        }

        var plan = new PlanEntity
        {
            Id = Id<PlanEntity>.New(),
            UserId = currentUser.Id,
            Name = name,
            IsActive = true,
            IsDeleted = false
        };

        currentUser.PlanId = plan.Id;
        await _planRepository.AddAsync(plan, cancellationToken);
        await _userRepository.UpdateAsync(currentUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }

     public async Task<Result<Unit, AppError>> UpdatePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, Id<PlanEntity> planId, string name, CancellationToken cancellationToken = default)
     {
         if (currentUser == null || routeUserId.IsEmpty)
         {
             return Result<Unit, AppError>.Failure(new InvalidPlanError(Messages.InvalidId));
         }

        if (currentUser.Id != routeUserId)
        {
            return Result<Unit, AppError>.Failure(new PlanForbiddenError(Messages.Forbidden));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Unit, AppError>.Failure(new InvalidPlanError(Messages.FieldRequired));
        }

         if (planId.IsEmpty)
         {
             return Result<Unit, AppError>.Failure(new InvalidPlanError(Messages.InvalidId));
         }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null)
        {
            return Result<Unit, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind));
        }

        plan.Name = name;
        await _planRepository.UpdateAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }

     public async Task<Result<PlanEntity, AppError>> GetPlanConfigAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default)
     {
         if (currentUser == null || routeUserId.IsEmpty)
         {
             return Result<PlanEntity, AppError>.Failure(new InvalidPlanError(Messages.InvalidId));
         }

        if (currentUser.Id != routeUserId)
        {
            return Result<PlanEntity, AppError>.Failure(new PlanForbiddenError(Messages.Forbidden));
        }

        var plan = await _planRepository.FindActiveByUserIdAsync(currentUser.Id, cancellationToken);
        if (plan == null)
        {
            return Result<PlanEntity, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind));
        }

        return Result<PlanEntity, AppError>.Success(plan);
    }

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

     public async Task<Result<List<PlanEntity>, AppError>> GetPlansListAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default)
     {
         if (currentUser == null || routeUserId.IsEmpty)
         {
             return Result<List<PlanEntity>, AppError>.Failure(new InvalidPlanError(Messages.InvalidId));
         }

        if (currentUser.Id != routeUserId)
        {
            return Result<List<PlanEntity>, AppError>.Failure(new PlanForbiddenError(Messages.Forbidden));
        }

        var plans = await _planRepository.GetByUserIdAsync(currentUser.Id, cancellationToken);
        if (plans.Count == 0)
        {
            return Result<List<PlanEntity>, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind));
        }

        return Result<List<PlanEntity>, AppError>.Success(plans);
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

     public async Task<Result<Unit, AppError>> DeletePlanAsync(UserEntity currentUser, Id<PlanEntity> planId, CancellationToken cancellationToken = default)
     {
         if (currentUser == null || planId.IsEmpty)
         {
             return Result<Unit, AppError>.Failure(new InvalidPlanError(Messages.InvalidId));
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
            await _planDayRepository.MarkDeletedByPlanIdAsync(plan.Id, cancellationToken);

            plan.IsActive = false;
            plan.IsDeleted = true;
            await _planRepository.UpdateAsync(plan, cancellationToken);

            var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)currentUser.Id, cancellationToken);
            if (user != null && user.PlanId == plan.Id)
            {
                var lastValidPlan = await _planRepository.FindLastActiveByUserIdAsync(currentUser.Id, cancellationToken);
                if (lastValidPlan != null)
                {
                    await _planRepository.SetActivePlanAsync(currentUser.Id, lastValidPlan.Id, cancellationToken);
                }

                user.PlanId = lastValidPlan?.Id;
                await _userRepository.UpdateAsync(user, cancellationToken);
                currentUser.PlanId = user.PlanId;
            }

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
