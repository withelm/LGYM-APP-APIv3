using System.Runtime.ExceptionServices;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Resources;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class RecurringReportAssignmentService
{
    public async Task<Result<RecurringReportAssignmentResult, AppError>> RequestNowAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<AccountReference> traineeId,
        Id<RecurringReportAssignment> assignmentId,
        CancellationToken cancellationToken = default)
    {
        var preflight = await ValidateRequestNowPreconditionsAsync(
            currentTrainer,
            traineeId,
            assignmentId,
            cancellationToken);
        if (preflight.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(preflight.Error);
        }

        var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var phase = TransactionPhase.PreCommit;
        Result<RecurringReportAssignmentResult, AppError>? result = null;
        ExceptionDispatchInfo? abort = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assignment = await _assignmentPersistence.FindByIdForUpdateAsync(
                assignmentId,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (assignment == null
                || assignment.TrainerId != currentTrainer.Id
                || assignment.TraineeId != traineeId)
            {
                result = RequestNowFailure(new ReportingNotFoundError(Messages.DidntFind));
                var rollback = await RollbackRequestNowDecisionAsync(transaction, cancellationToken);
                phase = rollback.Phase;
                abort = rollback.Abort;
            }
            else
            {
                var access = await _relationshipAccessPersistence.GetAccessAsync(
                    currentTrainer.Id,
                    traineeId,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                var episodeCheck = await StageAssignmentEpisodeRevisionCheckAsync(
                    assignment,
                    access,
                    cancellationToken);
                if (episodeCheck.IsFailure)
                {
                    result = RequestNowFailure(new ReportingNotFoundError(Messages.DidntFind));
                    var rollback = await RollbackRequestNowDecisionAsync(transaction, cancellationToken);
                    phase = rollback.Phase;
                    abort = rollback.Abort;
                }
                else if (assignment.Template.IsDeleted)
                {
                    var inactive = await InvalidatePendingScheduleStateAsync(
                        assignment,
                        currentTrainer.Id,
                        cancellationToken);
                    inactive = inactive with
                    {
                        IsActive = false,
                        ScheduleRevision = inactive.ScheduleRevision + 1
                    };
                    await _assignmentPersistence.UpdateAsync(
                        inactive.Id,
                        ToUpdateModel(inactive),
                        cancellationToken);
                    var saveResult = await SaveEpisodeFencedChangesAsync(transaction, cancellationToken);
                    if (saveResult.IsFailure)
                    {
                        phase = TransactionPhase.RolledBack;
                        result = RequestNowFailure(saveResult.Error);
                    }
                    else
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        phase = TransactionPhase.CommitStarted;
                        await transaction.CommitAsync(cancellationToken);
                        phase = TransactionPhase.Committed;
                        result = RequestNowFailure(
                            new ReportingConflictError(Messages.RecurringReportTemplateUnavailable));
                    }
                }
                else if (!IsWithinManualRequestWindow(assignment, DateTimeOffset.UtcNow))
                {
                    result = RequestNowFailure(
                        new ReportingConflictError(Messages.RecurringReportAssignmentUnavailable));
                    var rollback = await RollbackRequestNowDecisionAsync(transaction, cancellationToken);
                    phase = rollback.Phase;
                    abort = rollback.Abort;
                }
                else if (!IsManualRequestLifecycleEligible(assignment))
                {
                    result = RequestNowFailure(
                        new ReportingConflictError(Messages.RecurringReportRequestInProgress));
                    var rollback = await RollbackRequestNowDecisionAsync(transaction, cancellationToken);
                    phase = rollback.Phase;
                    abort = rollback.Abort;
                }
                else
                {
                    var stagedResult = await StageRequestCreationAsync(
                        assignment,
                        access,
                        DateTimeOffset.UtcNow,
                        cancellationToken);
                    if (stagedResult.IsFailure)
                    {
                        result = RequestNowFailure(stagedResult.Error);
                        var rollback = await RollbackRequestNowDecisionAsync(transaction, cancellationToken);
                        phase = rollback.Phase;
                        abort = rollback.Abort;
                    }
                    else
                    {
                        var staged = stagedResult.Value;
                        if (staged.EnvelopeId == null)
                        {
                            throw new InvalidOperationException(
                                "The recurring report notification envelope was not staged.");
                        }

                        RecurringReportAssignmentResult? mapped = null;
                        var revisionConflict = false;
                        try
                        {
                            await _unitOfWork.SaveChangesAsync(cancellationToken);
                        }
                        catch (Exception exception) when (_relationshipAccessPersistence.IsActiveEpisodeRevisionConflict(exception))
                        {
                            revisionConflict = true;
                        }

                        if (revisionConflict)
                        {
                            result = RequestNowFailure(new ReportingNotFoundError(Messages.DidntFind));
                            var rollback = await RollbackRequestNowDecisionAsync(transaction, cancellationToken);
                            phase = rollback.Phase;
                            abort = rollback.Abort;
                        }
                        else
                        {
                            var reloaded = await _assignmentPersistence.FindByIdAsync(
                                staged.Assignment.Id,
                                cancellationToken) ?? staged.Assignment;
                            mapped = MapAssignment(reloaded);
                            cancellationToken.ThrowIfCancellationRequested();
                            phase = TransactionPhase.CommitStarted;
                            await transaction.CommitAsync(cancellationToken);
                            phase = TransactionPhase.Committed;
                            result = Result<RecurringReportAssignmentResult, AppError>.Success(mapped);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException exception)
            when (phase == TransactionPhase.PreCommit && cancellationToken.IsCancellationRequested)
        {
            var rollbackFailure = await TryRollbackAsync(transaction);
            if (rollbackFailure == null)
            {
                phase = TransactionPhase.RolledBack;
                abort = ExceptionDispatchInfo.Capture(exception);
            }
            else
            {
                phase = TransactionPhase.RollbackFailed;
                abort = ExceptionDispatchInfo.Capture(rollbackFailure);
            }
        }
        catch (Exception exception) when (phase == TransactionPhase.PreCommit)
        {
            var rollbackFailure = await TryRollbackAsync(transaction);
            if (rollbackFailure == null)
            {
                phase = TransactionPhase.RolledBack;
                abort = ExceptionDispatchInfo.Capture(exception);
            }
            else
            {
                phase = TransactionPhase.RollbackFailed;
                abort = ExceptionDispatchInfo.Capture(rollbackFailure);
            }
        }
        catch (Exception exception) when (phase == TransactionPhase.CommitStarted)
        {
            abort = ExceptionDispatchInfo.Capture(exception);
        }

        await transaction.DisposeAsync();
        abort?.Throw();
        return result ?? throw new InvalidOperationException("The request-now transaction completed without a result.");
    }

    private async Task<Result<Unit, AppError>> ValidateRequestNowPreconditionsAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<AccountReference> traineeId,
        Id<RecurringReportAssignment> assignmentId,
        CancellationToken cancellationToken)
    {
        if (!currentTrainer.Roles.Contains(AuthConstants.Roles.Trainer, StringComparer.Ordinal))
        {
            return Result<Unit, AppError>.Failure(
                new ReportingForbiddenError(Messages.TrainerRoleRequired));
        }

        if (traineeId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(
                new InvalidReportingError(Messages.UserIdRequired));
        }

        if (assignmentId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(
                new InvalidReportingError(Messages.FieldRequired));
        }

        var access = await _relationshipAccessPersistence.GetAccessAsync(
            currentTrainer.Id,
            traineeId,
            cancellationToken);
        return access.HasActiveRelationship
            ? Result<Unit, AppError>.Success(Unit.Value)
            : Result<Unit, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
    }

    private async Task<Result<StagedRequestCreation, AppError>> StageRequestCreationAsync(
        RecurringReportAssignmentPersistenceModel assignment,
        ReportingRelationshipAccessFact relationshipAccess,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await _templatePersistence.LockTrainerDefinitionsAsync(assignment.TrainerId, cancellationToken);
        var currentTemplate = await _templatePersistence.FindByIdAsync(assignment.TemplateId, cancellationToken);
        if (currentTemplate is null || currentTemplate.IsDeleted)
            return Result<StagedRequestCreation, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        assignment = assignment with { Template = currentTemplate };
        var photoTypes = await ReportTemplateSnapshotFactory.EnsureDefaultsAsync(_templatePersistence, assignment.TrainerId, cancellationToken);
        var snapshotResult = ReportTemplateSnapshotFactory.Create(assignment.Template, photoTypes);
        if (snapshotResult.IsFailure)
        {
            return Result<StagedRequestCreation, AppError>.Failure(snapshotResult.Error);
        }

        var snapshotJson = ReportTemplateSnapshotFactory.Serialize(snapshotResult.Value);
        var request = new NewReportRequestPersistenceModel(
            Id<ReportRequest>.New(),
            assignment.TrainerId,
            assignment.TraineeId,
            assignment.TemplateId,
            assignment.Id,
            ReportRequestStatus.Pending,
            null,
            null,
            assignment.Note,
            createdAt,
            snapshotJson,
            relationshipAccess.ActiveEpisode!.EpisodeId);
        await _requestSubmissionPersistence.AddRequestAsync(request, cancellationToken);

        var updated = assignment with
        {
            CurrentReportRequestId = request.Id,
            CurrentReportRequest = ToRequestPersistenceModel(request, assignment.Template, snapshotJson),
            LastRequestCreatedAt = createdAt,
            NextEligibleAt = null,
            ScheduleRevision = assignment.ScheduleRevision + 1
        };
        var activeOverride = RecurringReportSchedulePolicy.ActiveOverride(assignment);
        if (activeOverride is not null)
        {
            await _assignmentPersistence.UpdateScheduleChangeResolutionAsync(
                activeOverride.Id,
                new RecurringReportScheduleChangeResolutionPersistenceModel(
                    RecurringReportScheduleChangeStatus.Consumed,
                    assignment.TrainerId,
                    createdAt),
                cancellationToken);
        }
        var pendingProposal = RecurringReportSchedulePolicy.PendingProposal(assignment);
        if (pendingProposal is not null)
        {
            await _assignmentPersistence.UpdateScheduleChangeResolutionAsync(
                pendingProposal.Id,
                new RecurringReportScheduleChangeResolutionPersistenceModel(
                    RecurringReportScheduleChangeStatus.Invalidated,
                    assignment.TrainerId,
                    createdAt),
                cancellationToken);
        }
        updated = updated with
        {
            ScheduleChanges = (assignment.ScheduleChanges ?? [])
                .Select(change => change == activeOverride
                    ? change with
                    {
                        Status = RecurringReportScheduleChangeStatus.Consumed,
                        ResolvedByAccountId = assignment.TrainerId,
                        ResolvedAt = createdAt
                    }
                    : pendingProposal is not null && change.Id == pendingProposal.Id
                        ? change with
                        {
                            Status = RecurringReportScheduleChangeStatus.Invalidated,
                            ResolvedByAccountId = assignment.TrainerId,
                            ResolvedAt = createdAt
                        }
                        : change)
                .ToList()
        };
        await _assignmentPersistence.UpdateAsync(updated.Id, ToUpdateModel(updated), cancellationToken);

        var envelope = await _commandOutboxWriter.StageAsync(new ReportRequestCreatedInAppNotificationCommand
        {
            RequestId = request.Id,
            TraineeId = assignment.TraineeId,
            TrainerId = assignment.TrainerId,
            TemplateName = assignment.Template.Name
        }, cancellationToken);
        return Result<StagedRequestCreation, AppError>.Success(new StagedRequestCreation(request.Id, envelope.EnvelopeId, updated));
    }

    private static Result<RecurringReportAssignmentResult, AppError> RequestNowFailure(AppError error)
        => Result<RecurringReportAssignmentResult, AppError>.Failure(error);

    private static async Task<RequestNowRollbackResult> RollbackRequestNowDecisionAsync(
        IUnitOfWorkTransaction transaction,
        CancellationToken cancellationToken)
    {
        var rollbackFailure = await TryRollbackAsync(transaction);
        if (rollbackFailure != null)
        {
            return new RequestNowRollbackResult(
                TransactionPhase.RollbackFailed,
                ExceptionDispatchInfo.Capture(rollbackFailure));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new RequestNowRollbackResult(
                TransactionPhase.RolledBack,
                ExceptionDispatchInfo.Capture(new OperationCanceledException(cancellationToken)));
        }

        return new RequestNowRollbackResult(TransactionPhase.RolledBack, null);
    }

    private sealed record RequestNowRollbackResult(
        TransactionPhase Phase,
        ExceptionDispatchInfo? Abort);
}
