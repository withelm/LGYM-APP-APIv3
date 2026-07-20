using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.TrainingPlanning.Plan.CheckIsUserHavePlan;
using LgymApi.Application.TrainingPlanning.Plan.CopyPlan;
using LgymApi.Application.TrainingPlanning.Plan.CreatePlan;
using LgymApi.Application.TrainingPlanning.Plan.DeletePlan;
using LgymApi.Application.TrainingPlanning.Plan.GenerateShareCode;
using LgymApi.Application.TrainingPlanning.Plan.GetPlanConfig;
using LgymApi.Application.TrainingPlanning.Plan.GetPlansList;
using LgymApi.Application.TrainingPlanning.Plan.Models;
using LgymApi.Application.TrainingPlanning.Plan.SetActivePlan;
using LgymApi.Application.TrainingPlanning.Plan.UpdatePlan;
using LgymApi.Domain.ValueObjects;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Plan;

public sealed class PlanService : IPlanService
{
    private readonly ICreatePlanUseCase _createPlanUseCase;
    private readonly IUpdatePlanUseCase _updatePlanUseCase;
    private readonly IDeletePlanUseCase _deletePlanUseCase;
    private readonly IGetPlanConfigUseCase _getPlanConfigUseCase;
    private readonly IGetPlansListUseCase _getPlansListUseCase;
    private readonly ISetActivePlanUseCase _setActivePlanUseCase;
    private readonly ICopyPlanUseCase _copyPlanUseCase;
    private readonly IGenerateShareCodeUseCase _generateShareCodeUseCase;
    private readonly ICheckIsUserHavePlanUseCase _checkIsUserHavePlanUseCase;

    public PlanService(
        ICreatePlanUseCase createPlanUseCase,
        IUpdatePlanUseCase updatePlanUseCase,
        IDeletePlanUseCase deletePlanUseCase,
        IGetPlanConfigUseCase getPlanConfigUseCase,
        IGetPlansListUseCase getPlansListUseCase,
        ISetActivePlanUseCase setActivePlanUseCase,
        ICopyPlanUseCase copyPlanUseCase,
        IGenerateShareCodeUseCase generateShareCodeUseCase,
        ICheckIsUserHavePlanUseCase checkIsUserHavePlanUseCase)
    {
        _createPlanUseCase = createPlanUseCase;
        _updatePlanUseCase = updatePlanUseCase;
        _deletePlanUseCase = deletePlanUseCase;
        _getPlanConfigUseCase = getPlanConfigUseCase;
        _getPlansListUseCase = getPlansListUseCase;
        _setActivePlanUseCase = setActivePlanUseCase;
        _copyPlanUseCase = copyPlanUseCase;
        _generateShareCodeUseCase = generateShareCodeUseCase;
        _checkIsUserHavePlanUseCase = checkIsUserHavePlanUseCase;
    }

    public Task<Result<Unit, AppError>> CreatePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, string name, CancellationToken cancellationToken = default)
        => _createPlanUseCase.ExecuteAsync(new CreatePlanCommand(GetCurrentUserId(currentUser), routeUserId, name), cancellationToken);

    public Task<Result<Unit, AppError>> UpdatePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, Id<PlanEntity> planId, string name, CancellationToken cancellationToken = default)
        => _updatePlanUseCase.ExecuteAsync(new UpdatePlanCommand(GetCurrentUserId(currentUser), routeUserId, planId, name), cancellationToken);

    public async Task<Result<PlanEntity, AppError>> GetPlanConfigAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default)
    {
        var result = await _getPlanConfigUseCase.ExecuteAsync(new GetPlanConfigQuery(GetCurrentUserId(currentUser), routeUserId), cancellationToken);
        return result.IsFailure
            ? Result<PlanEntity, AppError>.Failure(result.Error)
            : Result<PlanEntity, AppError>.Success(ToPlanEntity(result.Value));
    }

    public Task<Result<bool, AppError>> CheckIsUserHavePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default)
        => _checkIsUserHavePlanUseCase.ExecuteAsync(new CheckIsUserHavePlanQuery(GetCurrentUserId(currentUser), routeUserId), cancellationToken);

    public async Task<Result<List<PlanEntity>, AppError>> GetPlansListAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default)
    {
        var result = await _getPlansListUseCase.ExecuteAsync(new GetPlansListQuery(GetCurrentUserId(currentUser), routeUserId), cancellationToken);
        return result.IsFailure
            ? Result<List<PlanEntity>, AppError>.Failure(result.Error)
            : Result<List<PlanEntity>, AppError>.Success(result.Value.ConvertAll(ToPlanEntity));
    }

    public Task<Result<Unit, AppError>> SetNewActivePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, Id<PlanEntity> planId, CancellationToken cancellationToken = default)
        => _setActivePlanUseCase.ExecuteAsync(new SetActivePlanCommand(GetCurrentUserId(currentUser), routeUserId, planId), cancellationToken);

    public Task<Result<Unit, AppError>> DeletePlanAsync(UserEntity currentUser, Id<PlanEntity> planId, CancellationToken cancellationToken = default)
        => _deletePlanUseCase.ExecuteAsync(new DeletePlanCommand(GetCurrentUserId(currentUser), planId), cancellationToken);

    public async Task<Result<PlanEntity, AppError>> CopyPlanAsync(UserEntity currentUser, string shareCode, CancellationToken cancellationToken = default)
    {
        var result = await _copyPlanUseCase.ExecuteAsync(new CopyPlanCommand(GetCurrentUserId(currentUser), shareCode), cancellationToken);
        return result.IsFailure
            ? Result<PlanEntity, AppError>.Failure(result.Error)
            : Result<PlanEntity, AppError>.Success(ToPlanEntity(result.Value));
    }

    public Task<Result<string, AppError>> GenerateShareCodeAsync(UserEntity currentUser, Id<PlanEntity> planId, CancellationToken cancellationToken = default)
        => _generateShareCodeUseCase.ExecuteAsync(new GenerateShareCodeCommand(GetCurrentUserId(currentUser), planId), cancellationToken);

    private static Id<UserEntity> GetCurrentUserId(UserEntity? currentUser) => currentUser?.Id ?? Id<UserEntity>.Empty;

    private static PlanEntity ToPlanEntity(PlanReadModel plan) => new()
    {
        Id = plan.Id,
        UserId = plan.UserId,
        Name = plan.Name,
        IsActive = plan.IsActive,
        ShareCode = plan.ShareCode
    };
}
