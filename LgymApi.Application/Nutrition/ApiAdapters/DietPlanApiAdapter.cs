using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Nutrition.ApiAdapters;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Nutrition.DietPlans.ActivateTraineePlan.Contracts;
using LgymApi.Application.Nutrition.DietPlans.ActivateTraineePlan.Models;
using LgymApi.Application.Nutrition.DietPlans.CreateTraineePlan;
using LgymApi.Application.Nutrition.DietPlans.DeleteTraineePlan.Contracts;
using LgymApi.Application.Nutrition.DietPlans.DeleteTraineePlan.Models;
using LgymApi.Application.Nutrition.DietPlans.GetCurrentDietPlan;
using LgymApi.Application.Nutrition.DietPlans.GetCurrentDietPlans;
using LgymApi.Application.Nutrition.DietPlans.GetTraineePlan;
using LgymApi.Application.Nutrition.DietPlans.GetTraineePlanHistory.Contracts;
using LgymApi.Application.Nutrition.DietPlans.GetTraineePlanHistory.Models;
using LgymApi.Application.Nutrition.DietPlans.GetTraineePlans;
using LgymApi.Application.Nutrition.DietPlans.Models;
using LgymApi.Application.Nutrition.DietPlans.UpdateTraineePlan;
using LgymApi.Application.Nutrition.DietPlans.UpdateTraineePlan.Contracts;
using LgymApi.Application.Nutrition.DietPlans.UpdateOwnDietPlan;
using LgymApi.Application.Nutrition.DietPlans.UpdateOwnDietPlan.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;

namespace LgymApi.Application.Nutrition.ApiAdapters;

internal sealed class DietPlanApiAdapter : IDietPlanAccountApiAdapter
{
    private readonly IGetTraineeDietPlansUseCase _list;
    private readonly IGetTraineeDietPlanUseCase _get;
    private readonly ICreateTraineeDietPlanUseCase _create;
    private readonly IUpdateTraineeDietPlanUseCase _update;
    private readonly IUpdateOwnDietPlanUseCase _updateOwn;
    private readonly IActivateTraineeDietPlanUseCase _activate;
    private readonly IDeleteTraineeDietPlanUseCase _delete;
    private readonly IGetTraineeDietPlanHistoryUseCase _history;
    private readonly IGetCurrentDietPlansUseCase _currentPlans;
    private readonly IGetCurrentDietPlanUseCase _currentPlan;
    private readonly IMapper _mapper;
    private readonly IAuthenticatedAccountAuthorizationService _authorization;

    public DietPlanApiAdapter(IGetTraineeDietPlansUseCase list, IGetTraineeDietPlanUseCase get, ICreateTraineeDietPlanUseCase create, IUpdateTraineeDietPlanUseCase update, IUpdateOwnDietPlanUseCase updateOwn, IActivateTraineeDietPlanUseCase activate, IDeleteTraineeDietPlanUseCase delete, IGetTraineeDietPlanHistoryUseCase history, IGetCurrentDietPlansUseCase currentPlans, IGetCurrentDietPlanUseCase currentPlan, IMapper mapper, IAuthenticatedAccountAuthorizationService authorization)
    {
        _list = list;
        _get = get;
        _create = create;
        _update = update;
        _updateOwn = updateOwn;
        _activate = activate;
        _delete = delete;
        _history = history;
        _currentPlans = currentPlans;
        _currentPlan = currentPlan;
        _mapper = mapper;
        _authorization = authorization;
    }

    public Task<Result<IReadOnlyList<DietPlanReadModel>, AppError>> GetTraineePlansAsync(DietPlanListAccountQuery query, CancellationToken cancellationToken = default) => _list.ExecuteAsync(_mapper.Map<DietPlanListAccountQuery, GetTraineeDietPlansQuery>(query, CreateAuthorizationMappingContext(query.AccountContext)), cancellationToken);
    public Task<Result<DietPlanReadModel, AppError>> GetTraineePlanAsync(DietPlanGetAccountQuery query, CancellationToken cancellationToken = default) => _get.ExecuteAsync(_mapper.Map<DietPlanGetAccountQuery, GetTraineeDietPlanQuery>(query, CreateAuthorizationMappingContext(query.AccountContext)), cancellationToken);
    public Task<Result<IReadOnlyList<DietPlanReadModel>, AppError>> GetOwnPlansAsync(DietPlanCurrentAccountQuery query, CancellationToken cancellationToken = default) => _list.ExecuteAsync(_mapper.Map<DietPlanCurrentAccountQuery, GetTraineeDietPlansQuery>(query, CreateAuthorizationMappingContext(query.AccountContext)), cancellationToken);
    public Task<Result<DietPlanReadModel, AppError>> GetOwnPlanAsync(DietPlanOwnGetAccountQuery query, CancellationToken cancellationToken = default) => _get.ExecuteAsync(_mapper.Map<DietPlanOwnGetAccountQuery, GetTraineeDietPlanQuery>(query, CreateAuthorizationMappingContext(query.AccountContext)), cancellationToken);
    public Task<Result<IReadOnlyList<DietPlanHistoryReadModel>, AppError>> GetOwnHistoryAsync(DietPlanOwnGetAccountQuery query, CancellationToken cancellationToken = default) => _history.ExecuteAsync(_mapper.Map<DietPlanOwnGetAccountQuery, GetTraineeDietPlanHistoryQuery>(query, CreateAuthorizationMappingContext(query.AccountContext)), cancellationToken);
    public Task<Result<DietPlanReadModel, AppError>> CreateAsync(DietPlanCreateAccountCommand command, CancellationToken cancellationToken = default) => _create.ExecuteAsync(_mapper.Map<DietPlanCreateAccountCommand, CreateTraineeDietPlanCommand>(command, CreateAuthorizationMappingContext(command.AccountContext)), cancellationToken);
    public Task<Result<DietPlanReadModel, AppError>> UpdateAsync(DietPlanUpdateAccountCommand command, CancellationToken cancellationToken = default) => _update.ExecuteAsync(_mapper.Map<DietPlanUpdateAccountCommand, UpdateTraineeDietPlanCommand>(command, CreateAuthorizationMappingContext(command.AccountContext)), cancellationToken);
    public Task<Result<DietPlanReadModel, AppError>> UpdateOwnAsync(DietPlanOwnUpdateAccountCommand command, CancellationToken cancellationToken = default) => _updateOwn.ExecuteAsync(_mapper.Map<DietPlanOwnUpdateAccountCommand, UpdateOwnDietPlanCommand>(command, CreateAuthorizationMappingContext(command.AccountContext)), cancellationToken);
    public Task<Result<Unit, AppError>> ActivateAsync(DietPlanActivateAccountCommand command, CancellationToken cancellationToken = default) => _activate.ExecuteAsync(_mapper.Map<DietPlanActivateAccountCommand, ActivateTraineeDietPlanCommand>(command, CreateAuthorizationMappingContext(command.AccountContext)), cancellationToken);
    public Task<Result<Unit, AppError>> DeleteAsync(DietPlanDeleteAccountCommand command, CancellationToken cancellationToken = default) => _delete.ExecuteAsync(_mapper.Map<DietPlanDeleteAccountCommand, DeleteTraineeDietPlanCommand>(command, CreateAuthorizationMappingContext(command.AccountContext)), cancellationToken);
    public Task<Result<IReadOnlyList<DietPlanHistoryReadModel>, AppError>> GetHistoryAsync(DietPlanHistoryAccountQuery query, CancellationToken cancellationToken = default) => _history.ExecuteAsync(_mapper.Map<DietPlanHistoryAccountQuery, GetTraineeDietPlanHistoryQuery>(query, CreateAuthorizationMappingContext(query.AccountContext)), cancellationToken);
    public Task<Result<IReadOnlyList<DietPlanReadModel>, AppError>> GetCurrentPlansAsync(DietPlanCurrentAccountQuery query, CancellationToken cancellationToken = default) => _currentPlans.ExecuteAsync(_mapper.Map<DietPlanCurrentAccountQuery, GetCurrentDietPlansQuery>(query), cancellationToken);
    public Task<Result<DietPlanReadModel, AppError>> GetCurrentPlanAsync(DietPlanCurrentAccountQuery query, CancellationToken cancellationToken = default) => _currentPlan.ExecuteAsync(_mapper.Map<DietPlanCurrentAccountQuery, GetCurrentDietPlanQuery>(query), cancellationToken);

    private MappingContext CreateAuthorizationMappingContext(AuthenticatedAccountContext accountContext)
    {
        var context = _mapper.CreateContext();
        context.Set(NutritionApiAdapterMappingProfile.Keys.Actor, _authorization.CreateActor(accountContext));
        return context;
    }
}
