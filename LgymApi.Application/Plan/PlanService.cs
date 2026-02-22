using LgymApi.Application.Exceptions;
using LgymApi.Application.Repositories;
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

    public async Task CreatePlanAsync(UserEntity currentUser, Guid routeUserId, string name, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var plan = new PlanEntity
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.Id,
            Name = name,
            IsActive = true,
            IsDeleted = false
        };

        currentUser.PlanId = plan.Id;
        await _planRepository.AddAsync(plan, cancellationToken);
        await _userRepository.UpdateAsync(currentUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePlanAsync(UserEntity currentUser, Guid routeUserId, string planId, string name, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        if (!Guid.TryParse(planId, out var planGuid))
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var plan = await _planRepository.FindByIdAsync(planGuid, cancellationToken);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        plan.Name = name;
        await _planRepository.UpdateAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlanEntity> GetPlanConfigAsync(UserEntity currentUser, Guid routeUserId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var plan = await _planRepository.FindActiveByUserIdAsync(currentUser.Id, cancellationToken);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return plan;
    }

    public async Task<bool> CheckIsUserHavePlanAsync(UserEntity currentUser, Guid routeUserId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind, false);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden, false);
        }

        var plan = await _planRepository.FindActiveByUserIdAsync(currentUser.Id, cancellationToken);
        if (plan == null)
        {
            return false;
        }

        var planDayExists = await _planDayRepository.AnyByPlanIdAsync(plan.Id, cancellationToken);
        return planDayExists;
    }

    public async Task<List<PlanEntity>> GetPlansListAsync(UserEntity currentUser, Guid routeUserId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var plans = await _planRepository.GetByUserIdAsync(currentUser.Id, cancellationToken);
        if (plans.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return plans;
    }

    public async Task SetNewActivePlanAsync(UserEntity currentUser, Guid routeUserId, Guid planId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || routeUserId == Guid.Empty || planId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (plan.UserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _planRepository.SetActivePlanAsync(currentUser.Id, planId, cancellationToken);
            currentUser.PlanId = planId;
            await _userRepository.UpdateAsync(currentUser, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task DeletePlanAsync(UserEntity currentUser, Guid planId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (plan.UserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _planDayRepository.MarkDeletedByPlanIdAsync(plan.Id, cancellationToken);

            plan.IsActive = false;
            plan.IsDeleted = true;
            await _planRepository.UpdateAsync(plan, cancellationToken);

            var user = await _userRepository.FindByIdAsync(currentUser.Id, cancellationToken);
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
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PlanEntity> CopyPlanAsync(UserEntity currentUser, string shareCode, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.Unauthorized(Messages.Unauthorized);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var plan = await _planRepository.CopyPlanByShareCodeAsync(shareCode, currentUser.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return plan;
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw AppException.NotFound(Messages.DidntFind);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<string> GenerateShareCodeAsync(UserEntity currentUser, Guid planId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null || planId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        try
        {
            var shareCode = await _planRepository.GenerateShareCodeAsync(planId, currentUser.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return shareCode;
        }
        catch (KeyNotFoundException)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }
        catch (Exception exception) when (exception is InvalidOperationException || exception.GetType().Name == "DbUpdateException")
        {
            throw AppException.Internal(Messages.TryAgain);
        }
        catch (UnauthorizedAccessException)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }
    }
}
