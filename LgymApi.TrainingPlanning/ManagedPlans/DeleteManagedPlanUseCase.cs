using System.Data;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.TrainingPlanning.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Repositories;
using LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;
using LgymApi.Application.TrainingPlanning.Plan.ActivePlanPointer;
using LgymApi.Application.TrainingPlanning;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Application.TrainingPlanning.ManagedPlans;

internal sealed class DeleteManagedPlanUseCase : IDeleteManagedPlanUseCase
{
    private readonly IPlanRepository _planRepository;
    private readonly IActivePlanPointerStore _activePlanPointerStore;
    private readonly IAccountReadService _accountReadService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteManagedPlanUseCase(
        IPlanRepository planRepository,
        IActivePlanPointerStore activePlanPointerStore,
        IAccountReadService accountReadService,
        IUnitOfWork unitOfWork)
    {
        _planRepository = planRepository;
        _activePlanPointerStore = activePlanPointerStore;
        _accountReadService = accountReadService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit, AppError>> ExecuteAsync(
        DeleteManagedPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.TrainerId.IsEmpty || command.TraineeId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidPlanError(Messages.UserIdRequired));
        }

        if (command.PlanId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidPlanError(Messages.FieldRequired));
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var plan = await _planRepository.FindByIdAsync(command.PlanId, cancellationToken);
        if (plan is null || (plan.UserId != command.TrainerId.Rebind<LgymApi.Domain.Entities.User>() &&
            (plan.UserId != command.TraineeId.Rebind<LgymApi.Domain.Entities.User>() || plan.Purpose != PlanPurpose.Personal)))
        {
            return Result<Unit, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind));
        }

        if (await _accountReadService.GetByIdAsync(command.TraineeId, cancellationToken) is null)
        {
            return Result<Unit, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind));
        }

        try
        {
            plan.IsActive = false;
            plan.IsDeleted = true;
            await _planRepository.UpdateAsync(plan, cancellationToken);

            var activePlanId = await _activePlanPointerStore.GetActivePlanIdAsync(command.TraineeId, cancellationToken);
            if (activePlanId == plan.Id)
            {
                var activePlans = await _planRepository.GetActiveForUpdateAsync(command.TraineeId, cancellationToken);
                if (activePlans.Any(activePlan => activePlan.Id != plan.Id && activePlan.Purpose != PlanPurpose.Personal))
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return Result<Unit, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind));
                }
                await _activePlanPointerStore.StageActivePlanIdAsync(command.TraineeId, null, cancellationToken);
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
}
