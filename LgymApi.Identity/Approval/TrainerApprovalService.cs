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
            mapper.Map<Pagination<TrainerApplicationAttempt>, Pagination<TrainerApplication>>(result));
    }

    public async Task<Result<TrainerApplication, AppError>> GetAsync(
        AuthenticatedAccountContext actor, Id<AccountReference> accountId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsCurrentAdminAsync(actor, cancellationToken))
            return Result<TrainerApplication, AppError>.Failure(new ForbiddenError(Messages.Forbidden));
        var application = await applications.FindLatestAsync(accountId.Rebind<User>(), false, cancellationToken);
        return application is null
            ? Result<TrainerApplication, AppError>.Failure(new NotFoundError(Messages.DidntFind))
            : MapResult(application);
    }

    public async Task<Result<TrainerApplication, AppError>> DecideAsync(
        AuthenticatedAccountContext actor, Id<AccountReference> accountId, TrainerApprovalStatus decision,
        int attempt = 1, CancellationToken cancellationToken = default)
    {
        if (!await IsCurrentAdminAsync(actor, cancellationToken) || actor.Id == accountId)
            return Result<TrainerApplication, AppError>.Failure(new ForbiddenError(Messages.Forbidden));
        if (decision is not (TrainerApprovalStatus.Approved or TrainerApprovalStatus.Rejected))
            return Result<TrainerApplication, AppError>.Failure(new BadRequestError(Messages.TrainerApprovalDecisionInvalid));

        // Claim the pending transition before staging a role insert. Both saves belong
        // to one transaction; no caller can observe approval without the role/audit.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var application = await applications.FindLatestAsync(accountId.Rebind<User>(), true, cancellationToken);
        if (application is null)
            return Result<TrainerApplication, AppError>.Failure(new NotFoundError(Messages.DidntFind));
        if (application.Attempt != attempt)
            return Conflict();
        if (application.Status == decision)
            return MapResult(application);
        if (!CanUpdate(application, TrainerApprovalStatus.PendingApproval))
            return Conflict();

        var trainerRole = decision == TrainerApprovalStatus.Approved
            ? await roles.FindByNameAsync(AuthConstants.Roles.Trainer, cancellationToken) : null;
        if (decision == TrainerApprovalStatus.Approved && trainerRole is null)
            return Result<TrainerApplication, AppError>.Failure(new InternalServerError(Messages.DefaultRoleMissing));

        application.Status = decision;
        application.ReviewedBy = actor.Id.Rebind<User>();
        application.DecidedAt = DateTimeOffset.UtcNow;
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            if (trainerRole is not null)
            {
                await roles.AddUserRolesAsync(application.ApplicantId, [trainerRole.Id], cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict();
        }

        return MapResult(application);
    }

    public async Task<Result<Unit, AppError>> ResubmitAsync(
        AuthenticatedAccountContext actor, CancellationToken cancellationToken = default)
    {
        if (await ResolveCurrentAccountAsync(actor, cancellationToken) is null)
            return Result<Unit, AppError>.Failure(new UnauthorizedError(Messages.Unauthorized));

        var application = await applications.FindLatestAsync(actor.Id.Rebind<User>(), true, cancellationToken);
        if (application is { Attempt: > 1 } && CanUpdate(application, TrainerApprovalStatus.PendingApproval))
            return Result<Unit, AppError>.Success(Unit.Value);
        if (application is null || !CanUpdate(application, TrainerApprovalStatus.Rejected))
            return Result<Unit, AppError>.Failure(ApprovalConflict());

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        application.Revision++;
        try
        {
            // Claim this rejected attempt before inserting its successor.
            await unitOfWork.SaveChangesAsync(cancellationToken);
            applications.Add(TrainerApplicationAttempt.Submit(application.ApplicantId, application.Attempt + 1));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Unit, AppError>.Failure(ApprovalConflict());
        }
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task<AuthenticatedAccountContext?> ResolveCurrentAccountAsync(
        AuthenticatedAccountContext actor, CancellationToken cancellationToken)
    {
        if (actor.SessionId is not { } sessionId) return null;
        var current = await accounts.ResolveAsync(actor.Id, sessionId, cancellationToken);
        return current.Status == AuthenticatedAccountResolutionStatus.Active ? current.Context : null;
    }

    private async Task<bool> IsCurrentAdminAsync(AuthenticatedAccountContext actor, CancellationToken cancellationToken) =>
        (await ResolveCurrentAccountAsync(actor, cancellationToken))?.PermissionClaims
            .Contains(AuthConstants.Permissions.AdminAccess, StringComparer.Ordinal) == true;

    private static Result<TrainerApplication, AppError> Conflict() =>
        Result<TrainerApplication, AppError>.Failure(ApprovalConflict());

    private static TrainerApprovalError ApprovalConflict() =>
        new TrainerApprovalError("TrainerApprovalConflict", Messages.TrainerApprovalConflict, 409);

    private static bool CanUpdate(TrainerApplicationAttempt application, TrainerApprovalStatus expected) =>
        application.Status == expected && !application.Applicant.IsBlocked;

    private Result<TrainerApplication, AppError> MapResult(TrainerApplicationAttempt application) =>
        Result<TrainerApplication, AppError>.Success(mapper.Map<TrainerApplicationAttempt, TrainerApplication>(application));
}
