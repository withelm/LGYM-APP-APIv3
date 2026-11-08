using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Nutrition.DietPlans.GetTraineePlanHistory.Contracts;
using LgymApi.Application.Nutrition.DietPlans.GetTraineePlanHistory.Models;
using LgymApi.Application.Nutrition.DietPlans.Models;
using LgymApi.Application.Nutrition.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Resources;

namespace LgymApi.Application.Nutrition.DietPlans.GetTraineePlanHistory;

internal sealed class GetTraineeDietPlanHistoryUseCase : IGetTraineeDietPlanHistoryUseCase
{
    private readonly ICoachingRelationshipAccessService _relationshipAccess;
    private readonly IDietPlanPersistence _plans;
    private readonly IMapper _mapper;
    private readonly IDietPlanAuthorizationCoordinator _authorization;

    public GetTraineeDietPlanHistoryUseCase(
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

    public async Task<Result<IReadOnlyList<DietPlanHistoryReadModel>, AppError>> ExecuteAsync(
        GetTraineeDietPlanHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.TraineeId.IsEmpty)
        {
            return Result<IReadOnlyList<DietPlanHistoryReadModel>, AppError>.Failure(
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
            DietPlanAction.ReadHistory,
            actorId,
            query.TraineeId,
            access.IsTrainer,
            access.HasActiveRelationship);
        if (accessError is not null)
        {
            return Result<IReadOnlyList<DietPlanHistoryReadModel>, AppError>.Failure(accessError);
        }
        if (query.DietPlanId.IsEmpty)
        {
            return Result<IReadOnlyList<DietPlanHistoryReadModel>, AppError>.Failure(
                new BadRequestError(Messages.FieldRequired));
        }

        var plan = await _plans.GetPlanByIdAsync(query.DietPlanId, cancellationToken);
        if (plan is null || !DietPlanAccess.IsOwnedByTrainee(plan, query.TraineeId))
        {
            return Result<IReadOnlyList<DietPlanHistoryReadModel>, AppError>.Failure(
                new NotFoundError(Messages.DidntFind));
        }

        var history = await _plans.ListPlanHistoryAsync(plan.Id, cancellationToken);
        return Result<IReadOnlyList<DietPlanHistoryReadModel>, AppError>.Success(
            _mapper.MapList<DietPlanHistory, DietPlanHistoryReadModel>(history, _mapper.CreateContext()));
    }
}
