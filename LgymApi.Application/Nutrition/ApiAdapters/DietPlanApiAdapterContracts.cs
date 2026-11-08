using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Nutrition.DietPlans.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Application.Nutrition.ApiAdapters;

public interface IDietPlanAccountApiAdapter
{
    Task<Result<IReadOnlyList<DietPlanReadModel>, AppError>> GetTraineePlansAsync(DietPlanListAccountQuery query, CancellationToken cancellationToken = default);
    Task<Result<DietPlanReadModel, AppError>> GetTraineePlanAsync(DietPlanGetAccountQuery query, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DietPlanReadModel>, AppError>> GetOwnPlansAsync(DietPlanCurrentAccountQuery query, CancellationToken cancellationToken = default);
    Task<Result<DietPlanReadModel, AppError>> GetOwnPlanAsync(DietPlanOwnGetAccountQuery query, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DietPlanHistoryReadModel>, AppError>> GetOwnHistoryAsync(DietPlanOwnGetAccountQuery query, CancellationToken cancellationToken = default);
    Task<Result<DietPlanReadModel, AppError>> CreateAsync(DietPlanCreateAccountCommand command, CancellationToken cancellationToken = default);
    Task<Result<DietPlanReadModel, AppError>> UpdateAsync(DietPlanUpdateAccountCommand command, CancellationToken cancellationToken = default);
    Task<Result<DietPlanReadModel, AppError>> UpdateOwnAsync(DietPlanOwnUpdateAccountCommand command, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> ActivateAsync(DietPlanActivateAccountCommand command, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> DeleteAsync(DietPlanDeleteAccountCommand command, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DietPlanHistoryReadModel>, AppError>> GetHistoryAsync(DietPlanHistoryAccountQuery query, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DietPlanReadModel>, AppError>> GetCurrentPlansAsync(DietPlanCurrentAccountQuery query, CancellationToken cancellationToken = default);
    Task<Result<DietPlanReadModel, AppError>> GetCurrentPlanAsync(DietPlanCurrentAccountQuery query, CancellationToken cancellationToken = default);
}

public sealed record DietPlanListAccountQuery(AuthenticatedAccountContext AccountContext, Id<AccountReference> TraineeId);
public sealed record DietPlanGetAccountQuery(AuthenticatedAccountContext AccountContext, Id<AccountReference> TraineeId, Id<DietPlan> DietPlanId);
public sealed record DietPlanOwnGetAccountQuery(AuthenticatedAccountContext AccountContext, Id<DietPlan> DietPlanId);
public sealed record DietPlanCreateAccountCommand(AuthenticatedAccountContext AccountContext, Id<AccountReference> TraineeId, DietPlanUpsertData Data);
public sealed record DietPlanUpdateAccountCommand(AuthenticatedAccountContext AccountContext, Id<AccountReference> TraineeId, Id<DietPlan> DietPlanId, DietPlanUpsertData Data);
public sealed record DietPlanOwnUpdateAccountCommand(AuthenticatedAccountContext AccountContext, Id<DietPlan> DietPlanId, DietPlanUpsertData Data);
public sealed record DietPlanActivateAccountCommand(AuthenticatedAccountContext AccountContext, Id<AccountReference> TraineeId, Id<DietPlan> DietPlanId);
public sealed record DietPlanDeleteAccountCommand(AuthenticatedAccountContext AccountContext, Id<AccountReference> TraineeId, Id<DietPlan> DietPlanId);
public sealed record DietPlanHistoryAccountQuery(AuthenticatedAccountContext AccountContext, Id<AccountReference> TraineeId, Id<DietPlan> DietPlanId);
public sealed record DietPlanCurrentAccountQuery(AuthenticatedAccountContext AccountContext);
