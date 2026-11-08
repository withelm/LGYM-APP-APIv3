using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Nutrition.Supplementation.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Application.Nutrition.ApiAdapters;

public interface ISupplementationApiAdapter
{
    Task<Result<IReadOnlyList<SupplementPlanReadModel>, AppError>> GetTraineePlansAsync(SupplementPlanListAccountQuery query, CancellationToken cancellationToken = default);
    Task<Result<SupplementPlanReadModel, AppError>> CreateAsync(SupplementPlanCreateAccountCommand command, CancellationToken cancellationToken = default);
    Task<Result<SupplementPlanReadModel, AppError>> UpdateAsync(SupplementPlanUpdateAccountCommand command, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> DeleteAsync(SupplementPlanDeleteAccountCommand command, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> AssignAsync(SupplementPlanAssignAccountCommand command, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> UnassignAsync(SupplementPlanUnassignAccountCommand command, CancellationToken cancellationToken = default);
    Task<Result<SupplementComplianceSummaryReadModel, AppError>> GetComplianceAsync(SupplementComplianceAccountQuery query, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SupplementScheduleEntryReadModel>, AppError>> GetScheduleAsync(SupplementScheduleAccountQuery query, CancellationToken cancellationToken = default);
    Task<Result<SupplementScheduleEntryReadModel, AppError>> CheckOffAsync(SupplementCheckOffAccountCommand command, CancellationToken cancellationToken = default);
}

public sealed record SupplementPlanListAccountQuery(AuthenticatedAccountContext AccountContext, Id<AccountReference> TraineeId);
public sealed record SupplementPlanCreateAccountCommand(AuthenticatedAccountContext AccountContext, Id<AccountReference> TraineeId, SupplementPlanUpsertData Data);
public sealed record SupplementPlanUpdateAccountCommand(AuthenticatedAccountContext AccountContext, Id<AccountReference> TraineeId, Id<SupplementPlan> PlanId, SupplementPlanUpsertData Data);
public sealed record SupplementPlanDeleteAccountCommand(AuthenticatedAccountContext AccountContext, Id<AccountReference> TraineeId, Id<SupplementPlan> PlanId);
public sealed record SupplementPlanAssignAccountCommand(AuthenticatedAccountContext AccountContext, Id<AccountReference> TraineeId, Id<SupplementPlan> PlanId);
public sealed record SupplementPlanUnassignAccountCommand(AuthenticatedAccountContext AccountContext, Id<AccountReference> TraineeId);
public sealed record SupplementComplianceAccountQuery(AuthenticatedAccountContext AccountContext, Id<AccountReference> TraineeId, DateOnly FromDate, DateOnly ToDate);
public sealed record SupplementScheduleAccountQuery(AuthenticatedAccountContext AccountContext, DateOnly IntakeDate);
public sealed record SupplementCheckOffAccountCommand(AuthenticatedAccountContext AccountContext, Id<SupplementPlanItem> PlanItemId, DateOnly IntakeDate, DateTimeOffset? TakenAt);
