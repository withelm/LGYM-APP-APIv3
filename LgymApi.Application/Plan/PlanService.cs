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

    public async Task CreatePlanAsync(UserEntity currentUser, Guid routeUserId, string name)
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
        await _planRepository.AddAsync(plan);
        await _userRepository.UpdateAsync(currentUser);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UpdatePlanAsync(UserEntity currentUser, Guid routeUserId, string planId, string name)
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

        var plan = await _planRepository.FindByIdAsync(planGuid);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        plan.Name = name;
        await _planRepository.UpdateAsync(plan);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PlanEntity> GetPlanConfigAsync(UserEntity currentUser, Guid routeUserId)
    {
        if (currentUser == null || routeUserId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var plan = await _planRepository.FindActiveByUserIdAsync(currentUser.Id);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return plan;
    }

    public async Task<bool> CheckIsUserHavePlanAsync(UserEntity currentUser, Guid routeUserId)
    {
        if (currentUser == null || routeUserId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind, false);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden, false);
        }

        var plan = await _planRepository.FindActiveByUserIdAsync(currentUser.Id);
        if (plan == null)
        {
            return false;
        }

        var planDayExists = await _planDayRepository.AnyByPlanIdAsync(plan.Id);
        return planDayExists;
    }

    public async Task<List<PlanEntity>> GetPlansListAsync(UserEntity currentUser, Guid routeUserId)
    {
        if (currentUser == null || routeUserId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var plans = await _planRepository.GetByUserIdAsync(currentUser.Id);
        if (plans.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return plans;
    }

    public async Task SetNewActivePlanAsync(UserEntity currentUser, Guid routeUserId, Guid planId)
    {
        if (currentUser == null || routeUserId == Guid.Empty || planId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var plan = await _planRepository.FindByIdAsync(planId);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (plan.UserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        await _planRepository.SetActivePlanAsync(currentUser.Id, planId);
        currentUser.PlanId = planId;
        await _userRepository.UpdateAsync(currentUser);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeletePlanAsync(UserEntity currentUser, Guid planId)
    {
        if (currentUser == null || planId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var plan = await _planRepository.FindByIdAsync(planId);
        if (plan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (plan.UserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _planDayRepository.MarkDeletedByPlanIdAsync(plan.Id);

            plan.IsActive = false;
            plan.IsDeleted = true;
            await _planRepository.UpdateAsync(plan);

            var user = await _userRepository.FindByIdAsync(currentUser.Id);
            if (user != null && user.PlanId == plan.Id)
            {
                var lastValidPlan = await _planRepository.FindLastActiveByUserIdAsync(currentUser.Id);
                if (lastValidPlan != null)
                {
                    await _planRepository.SetActivePlanAsync(currentUser.Id, lastValidPlan.Id);
                }

                user.PlanId = lastValidPlan?.Id;
                await _userRepository.UpdateAsync(user);
                currentUser.PlanId = user.PlanId;
            }

            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<PlanEntity> CopyPlanAsync(UserEntity currentUser, string shareCode)
    {
        if (currentUser == null)
        {
            throw AppException.Unauthorized(Messages.Unauthorized);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            var plan = await _planRepository.CopyPlanByShareCodeAsync(shareCode, currentUser.Id);
            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
            return plan;
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync();
            throw AppException.NotFound(Messages.DidntFind);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<string> GenerateShareCodeAsync(UserEntity currentUser, Guid planId)
    {
        if (currentUser == null || planId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        try
        {
            var shareCode = await _planRepository.GenerateShareCodeAsync(planId, currentUser.Id);
            await _unitOfWork.SaveChangesAsync();
            return shareCode;
        }
        catch (InvalidOperationException)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }
        catch (UnauthorizedAccessException)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }
    }
}
