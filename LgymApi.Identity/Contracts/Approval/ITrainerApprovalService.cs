using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Pagination;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Identity.Contracts.Approval;

public sealed record TrainerApplication(
    Id<AccountReference> AccountId,
    string Name,
    string Email,
    TrainerApprovalStatus Status,
    DateTimeOffset CreatedAt,
    Id<AccountReference>? ReviewedBy,
    DateTimeOffset? DecidedAt);

public interface ITrainerApprovalService
{
    Task<Result<Pagination<TrainerApplication>, AppError>> ListAsync(
        AuthenticatedAccountContext actor, int page, int pageSize, bool includeReviewed,
        CancellationToken cancellationToken = default);

    Task<Result<TrainerApplication, AppError>> GetAsync(
        AuthenticatedAccountContext actor, Id<AccountReference> accountId,
        CancellationToken cancellationToken = default);

    Task<Result<TrainerApplication, AppError>> DecideAsync(
        AuthenticatedAccountContext actor, Id<AccountReference> accountId, TrainerApprovalStatus decision,
        CancellationToken cancellationToken = default);
}
