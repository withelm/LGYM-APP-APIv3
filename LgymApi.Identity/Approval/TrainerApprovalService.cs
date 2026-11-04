using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Approval;
using LgymApi.Resources;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Identity.Approval;

internal sealed class TrainerApprovalService(
    ITrainerApplicationRepository applications,
    IRoleRepository roles,
    IAuthenticatedAccountContextResolver accounts,
    IUnitOfWork unitOfWork,
    IMapper mapper) : ITrainerApprovalService
{
    public async Task<Result<Pagination<TrainerApplication>, AppError>> ListAsync(
        AuthenticatedAccountContext actor, int page, int pageSize, bool includeReviewed,
        CancellationToken cancellationToken = default)
    {
        if (!await IsCurrentAdminAsync(actor, cancellationToken))
            return Result<Pagination<TrainerApplication>, AppError>.Failure(new ForbiddenError(Messages.Forbidden));
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            return Result<Pagination<TrainerApplication>, AppError>.Failure(new BadRequestError(Messages.TrainerApplicationPagingInvalid));

        var result = await applications.ListAsync(page, pageSize, includeReviewed, cancellationToken);
        return Result<Pagination<TrainerApplication>, AppError>.Success(
            mapper.Map<Pagination<User>, Pagination<TrainerApplication>>(result));
    }

    public async Task<Result<TrainerApplication, AppError>> GetAsync(
        AuthenticatedAccountContext actor, Id<AccountReference> accountId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsCurrentAdminAsync(actor, cancellationToken))
            return Result<TrainerApplication, AppError>.Failure(new ForbiddenError(Messages.Forbidden));
        var user = await applications.FindAsync(accountId.Rebind<User>(), false, cancellationToken);
        return user is null
            ? Result<TrainerApplication, AppError>.Failure(new NotFoundError(Messages.DidntFind))
            : Result<TrainerApplication, AppError>.Success(mapper.Map<User, TrainerApplication>(user));
    }

    public async Task<Result<TrainerApplication, AppError>> DecideAsync(
        AuthenticatedAccountContext actor, Id<AccountReference> accountId, TrainerApprovalStatus decision,
        CancellationToken cancellationToken = default)
    {
        if (!await IsCurrentAdminAsync(actor, cancellationToken) || actor.Id == accountId)
            return Result<TrainerApplication, AppError>.Failure(new ForbiddenError(Messages.Forbidden));
        if (decision is not (TrainerApprovalStatus.Approved or TrainerApprovalStatus.Rejected))
            return Result<TrainerApplication, AppError>.Failure(new BadRequestError(Messages.TrainerApprovalDecisionInvalid));

        // Claim the pending transition before staging a role insert. Both saves belong
        // to one transaction; no caller can observe approval without the role/audit.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var user = await applications.FindAsync(accountId.Rebind<User>(), true, cancellationToken);
        if (user is null)
            return Result<TrainerApplication, AppError>.Failure(new NotFoundError(Messages.DidntFind));
        if (user.TrainerApprovalStatus == decision)
            return Result<TrainerApplication, AppError>.Success(mapper.Map<User, TrainerApplication>(user));
        if (user.TrainerApprovalStatus != TrainerApprovalStatus.PendingApproval || user.IsBlocked)
            return Conflict();

        var trainerRole = decision == TrainerApprovalStatus.Approved
            ? await roles.FindByNameAsync(AuthConstants.Roles.Trainer, cancellationToken) : null;
        if (decision == TrainerApprovalStatus.Approved && trainerRole is null)
            return Result<TrainerApplication, AppError>.Failure(new InternalServerError(Messages.DefaultRoleMissing));

        user.TrainerApprovalStatus = decision;
        user.TrainerApprovalReviewedBy = actor.Id.Rebind<User>();
        user.TrainerApprovalDecidedAt = DateTimeOffset.UtcNow;
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            if (trainerRole is not null)
            {
                await roles.AddUserRolesAsync(user.Id, [trainerRole.Id], cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict();
        }

        return Result<TrainerApplication, AppError>.Success(mapper.Map<User, TrainerApplication>(user));
    }

    private async Task<bool> IsCurrentAdminAsync(AuthenticatedAccountContext actor, CancellationToken cancellationToken)
    {
        if (actor.SessionId is not { } sessionId) return false;
        var current = await accounts.ResolveAsync(actor.Id, sessionId, cancellationToken);
        return current.Status == AuthenticatedAccountResolutionStatus.Active
            && current.Context?.PermissionClaims.Contains(AuthConstants.Permissions.AdminAccess, StringComparer.Ordinal) == true;
    }

    private static Result<TrainerApplication, AppError> Conflict() =>
        Result<TrainerApplication, AppError>.Failure(new TrainerApprovalError(
            "TrainerApprovalConflict", Messages.TrainerApprovalConflict, 409));
}
