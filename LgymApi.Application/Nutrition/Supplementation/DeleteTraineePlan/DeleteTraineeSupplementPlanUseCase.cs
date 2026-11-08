using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Nutrition.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Nutrition.Persistence;
using LgymApi.Application.Nutrition.Supplementation.DeleteTraineePlan.Contracts;
using LgymApi.Application.Nutrition.Supplementation.DeleteTraineePlan.Models;
using LgymApi.Application.Repositories;
using LgymApi.Resources;

namespace LgymApi.Application.Nutrition.Supplementation.DeleteTraineePlan;

internal sealed class DeleteTraineeSupplementPlanUseCase : IDeleteTraineeSupplementPlanUseCase
{
    private readonly ICoachingRelationshipAccessService _relationshipAccess;
    private readonly ISupplementationPersistence _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISupplementationAuthorizationCoordinator _authorization;

    public DeleteTraineeSupplementPlanUseCase(
        ICoachingRelationshipAccessService relationshipAccess,
        ISupplementationPersistence plans,
        IUnitOfWork unitOfWork,
        ISupplementationAuthorizationCoordinator authorization)
    {
        _relationshipAccess = relationshipAccess;
        _plans = plans;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result<Unit, AppError>> ExecuteAsync(
        DeleteTraineeSupplementPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        var actorId = command.Actor.Id.Rebind<LgymApi.Domain.Entities.User>();
        var access = await _relationshipAccess.GetAccessDecisionAsync(
            actorId,
            command.TraineeId,
            cancellationToken);
        var accessError = SupplementationAccess.GetTrainerAccessError(
            _authorization,
            command.Actor,
            access.IsTrainer,
            access.HasActiveRelationship,
            command.TraineeId);
        if (accessError is not null)
        {
            return Result<Unit, AppError>.Failure(accessError);
        }

        if (command.SupplementPlanId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidSupplementationError(Messages.FieldRequired));
        }

        var plan = await _plans.FindTrackedPlanByIdAsync(command.SupplementPlanId, cancellationToken);
        if (plan is null || !SupplementationAccess.IsOwnedBy(plan, actorId, command.TraineeId))
        {
            return Result<Unit, AppError>.Failure(new SupplementationNotFoundError(Messages.DidntFind));
        }

        plan.IsDeleted = true;
        plan.IsActive = false;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }
}
