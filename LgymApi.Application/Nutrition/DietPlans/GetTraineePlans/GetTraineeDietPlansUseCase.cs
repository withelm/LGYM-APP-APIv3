using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Nutrition.DietPlans.Models;
using LgymApi.Application.Nutrition.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Resources;

namespace LgymApi.Application.Nutrition.DietPlans.GetTraineePlans;

internal sealed class GetTraineeDietPlansUseCase : IGetTraineeDietPlansUseCase
{
    private readonly ICoachingRelationshipAccessService _relationshipAccess;
    private readonly IDietPlanPersistence _plans;
    private readonly IMapper _mapper;
    private readonly IDietPlanAuthorizationCoordinator _authorization;

    public GetTraineeDietPlansUseCase(
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

    public async Task<Result<IReadOnlyList<DietPlanReadModel>, AppError>> ExecuteAsync(
        GetTraineeDietPlansQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.TraineeId.IsEmpty)
        {
            return Result<IReadOnlyList<DietPlanReadModel>, AppError>.Failure(new BadRequestError(Messages.UserIdRequired));
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
            return Result<IReadOnlyList<DietPlanReadModel>, AppError>.Failure(accessError);
        }

        var plans = await _plans.ListPlansByTraineeAsync(query.TraineeId, cancellationToken);
        var readModels = _mapper.MapList<DietPlan, DietPlanReadModel>(plans, _mapper.CreateContext());
        return Result<IReadOnlyList<DietPlanReadModel>, AppError>.Success(readModels);
    }
}
