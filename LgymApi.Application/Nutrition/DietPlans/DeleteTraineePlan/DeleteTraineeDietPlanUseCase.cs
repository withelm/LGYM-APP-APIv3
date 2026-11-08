using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Nutrition.DietPlans.DeleteTraineePlan.Contracts;
using LgymApi.Application.Nutrition.DietPlans.DeleteTraineePlan.Models;
using LgymApi.Application.Nutrition.Persistence;
using LgymApi.Application.Repositories;
using LgymApi.Resources;

namespace LgymApi.Application.Nutrition.DietPlans.DeleteTraineePlan;

internal sealed class DeleteTraineeDietPlanUseCase : IDeleteTraineeDietPlanUseCase
{
    private readonly ICoachingRelationshipAccessService _relationshipAccess;
    private readonly IDietPlanPersistence _plans;
    private readonly DietPlanHistorySnapshotFactory _historyFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDietPlanAuthorizationCoordinator _authorization;

    public DeleteTraineeDietPlanUseCase(
        ICoachingRelationshipAccessService relationshipAccess,
        IDietPlanPersistence plans,
        DietPlanHistorySnapshotFactory historyFactory,
        IUnitOfWork unitOfWork,
        IDietPlanAuthorizationCoordinator authorization)
    {
        _relationshipAccess = relationshipAccess;
        _plans = plans;
        _historyFactory = historyFactory;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result<Unit, AppError>> ExecuteAsync(
        DeleteTraineeDietPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.TraineeId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new BadRequestError(Messages.UserIdRequired));
        }

        var actorId = command.Actor.Id.Rebind<LgymApi.Domain.Entities.User>();
        var access = await _relationshipAccess.GetAccessDecisionAsync(
            actorId,
            command.TraineeId,
            cancellationToken);
        var accessError = DietPlanAccess.GetAccessError(
            _authorization,
            command.Actor,
            DietPlanAction.Delete,
            actorId,
            command.TraineeId,
            access.IsTrainer,
            access.HasActiveRelationship);
        if (accessError is not null)
        {
            return Result<Unit, AppError>.Failure(accessError);
        }

        if (command.DietPlanId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new BadRequestError(Messages.FieldRequired));
        }

        var plan = await _plans.FindTrackedPlanByIdAsync(command.DietPlanId, cancellationToken);
        if (plan is null || !DietPlanAccess.IsOwnedByTrainee(plan, command.TraineeId))
        {
            return Result<Unit, AppError>.Failure(new NotFoundError(Messages.DidntFind));
        }

        plan.IsDeleted = true;
        plan.IsActive = false;
        await _plans.AddHistoryEntryAsync(
            _historyFactory.Create(plan, actorId, "Deleted"),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }
}
