using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Nutrition.DietPlans.Models;
using LgymApi.Application.Nutrition.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Resources;

namespace LgymApi.Application.Nutrition.DietPlans.GetTraineePlan;

internal sealed class GetTraineeDietPlanUseCase : IGetTraineeDietPlanUseCase
{
    private readonly ICoachingRelationshipAccessService _relationshipAccess;
    private readonly IDietPlanPersistence _plans;
    private readonly IMapper _mapper;
    private readonly IDietPlanAuthorizationCoordinator _authorization;

    public GetTraineeDietPlanUseCase(
        ICoachingRelationshipAccessService relationshipAccess,
        IDietPlanPersistence plans,
        IMapper mapper,
        IDietPlanAuthorizationCoordinator authorization)
    {
        _relationshipAccess = relationshipAccess;
        _plans = plans;
        _mapper = mapper;
        _authorization = authorization;
    }

    public async Task<Result<DietPlanReadModel, AppError>> ExecuteAsync(
        GetTraineeDietPlanQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.TraineeId.IsEmpty)
        {
            return Result<DietPlanReadModel, AppError>.Failure(
                new BadRequestError(Messages.UserIdRequired));
        }

        var actorId = query.Actor.Id.Rebind<User>();
        var access = await _relationshipAccess.GetAccessDecisionAsync(
            actorId,
            query.TraineeId,
            cancellationToken);
        var accessError = DietPlanAccess.GetAccessError(
            _authorization,
            query.Actor,
            DietPlanAction.Read,
            actorId,
            query.TraineeId,
            access.IsTrainer,
            access.HasActiveRelationship);
        if (accessError is not null)
        {
            return Result<DietPlanReadModel, AppError>.Failure(accessError);
        }
        if (query.DietPlanId.IsEmpty)
        {
            return Result<DietPlanReadModel, AppError>.Failure(
                new BadRequestError(Messages.FieldRequired));
        }

        var plan = await _plans.GetPlanByIdAsync(query.DietPlanId, cancellationToken);
        if (plan is null || !DietPlanAccess.IsOwnedByTrainee(plan, query.TraineeId))
        {
            return Result<DietPlanReadModel, AppError>.Failure(
                new NotFoundError(Messages.DidntFind));
        }

        return Result<DietPlanReadModel, AppError>.Success(
            _mapper.Map<DietPlan, DietPlanReadModel>(plan, _mapper.CreateContext()));
    }
}
