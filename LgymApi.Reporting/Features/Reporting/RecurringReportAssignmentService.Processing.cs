using System.Runtime.ExceptionServices;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class RecurringReportAssignmentService
{
    public async Task ProcessDueAssignmentsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var candidates = await _assignmentPersistence.ListDueAsync(now, cancellationToken);

        foreach (var candidate in candidates)
        {
            if (CreateDueReminder(candidate, now) is not null)
            {
                await ProcessDueReminderAsync(candidate.Id, now, cancellationToken);
            }
        }

        foreach (var candidate in candidates)
        {
            await ProcessCandidateAsync(candidate.Id, now, cancellationToken);
        }
    }

    private async Task ProcessDueReminderAsync(
        Id<RecurringReportAssignment> assignmentId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var assignment = await _assignmentPersistence.FindByIdForUpdateAsync(assignmentId, cancellationToken);
        if (assignment is null)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return;
        }

        var reminder = CreateDueReminder(assignment, now);
        if (reminder is null)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return;
        }

        var relationshipAccess = await _relationshipAccessPersistence.GetAccessAsync(
            assignment.TrainerId,
            assignment.TraineeId,
            cancellationToken);
        var episodeCheck = await StageAssignmentEpisodeRevisionCheckAsync(
            assignment,
            relationshipAccess,
            cancellationToken);
        if (episodeCheck.IsFailure)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return;
        }

        if (reminder.IsUpcomingRequestReminder)
        {
            await _templatePersistence.LockTrainerDefinitionsAsync(assignment.TrainerId, cancellationToken);
            var template = await _templatePersistence.FindByIdAsync(assignment.TemplateId, cancellationToken);
            if (template is null || template.IsDeleted)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return;
            }

            var photoTypes = await ReportTemplateSnapshotFactory.EnsureDefaultsAsync(
                _templatePersistence, assignment.TrainerId, cancellationToken, stageDefaults: false);
            if (ReportTemplateSnapshotFactory.Create(template, photoTypes).IsFailure)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return;
            }
        }

        await _commandOutboxWriter.StageAsync(reminder, cancellationToken);
        var saveResult = await SaveEpisodeFencedChangesAsync(transaction, cancellationToken);
        if (saveResult.IsFailure)
        {
            return;
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static RecurringReportReminderInAppNotificationCommand? CreateDueReminder(
        RecurringReportAssignmentPersistenceModel assignment,
        DateTimeOffset now)
    {
        if (!assignment.IsActive || assignment.IsDeleted)
        {
            return null;
        }

        if ((assignment.NextEligibleAt ?? RecalculateNextEligibleAt(assignment)) is { } nextEligibleAt
            && nextEligibleAt > now
            && now >= nextEligibleAt.AddHours(-24)
            && !assignment.Template.IsDeleted
            && EvaluateAutomaticEligibility(assignment, nextEligibleAt).CanCreate)
        {
            return new RecurringReportReminderInAppNotificationCommand
            {
                TraineeId = assignment.TraineeId,
                TrainerId = assignment.TrainerId,
                AssignmentId = assignment.Id,
                ScheduleRevision = assignment.ScheduleRevision,
                ScheduledFor = nextEligibleAt,
                IsUpcomingRequestReminder = true
            };
        }

        var request = assignment.CurrentReportRequest;
        if (request is null
            || request.Status != ReportRequestStatus.Pending
            || request.CreatedAt.AddHours(48) > now)
        {
            return null;
        }

        return new RecurringReportReminderInAppNotificationCommand
        {
            TraineeId = assignment.TraineeId,
            TrainerId = assignment.TrainerId,
            AssignmentId = assignment.Id,
            ScheduleRevision = assignment.ScheduleRevision,
            ScheduledFor = request.CreatedAt.AddHours(48),
            ReportRequestId = request.Id,
            IsUpcomingRequestReminder = false
        };
    }

    private async Task ProcessCandidateAsync(
        Id<RecurringReportAssignment> assignmentId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        IUnitOfWorkTransaction transaction;
        try
        {
            transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            LogAborted(assignmentId, ProcessingReason.BeginTransactionFailed, exception.GetType().Name);
            throw;
        }

        var phase = TransactionPhase.PreCommit;
        ProcessingResult? result = null;
        ExceptionDispatchInfo? abort = null;
        var preCommitReason = ProcessingReason.PreCommitFailed;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assignment = await _assignmentPersistence.FindByIdForUpdateAsync(assignmentId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (assignment == null)
            {
                var rollback = await RollbackBusinessDecisionAsync(
                    transaction,
                    assignmentId,
                    AutomaticEligibilityReason.AssignmentMissing,
                    cancellationToken);
                result = rollback.Result;
                abort = rollback.Failure == null ? null : ExceptionDispatchInfo.Capture(rollback.Failure);
                phase = rollback.Failure == null
                    ? TransactionPhase.RolledBack
                    : TransactionPhase.RollbackFailed;
            }
            else
            {
                var eligibility = EvaluateAutomaticEligibility(assignment, now);
                if (!eligibility.CanCreate)
                {
                    var rollback = await RollbackBusinessDecisionAsync(
                        transaction,
                        assignmentId,
                        eligibility.Reason,
                        cancellationToken);
                    result = rollback.Result;
                    abort = rollback.Failure == null ? null : ExceptionDispatchInfo.Capture(rollback.Failure);
                    phase = rollback.Failure == null
                        ? TransactionPhase.RolledBack
                        : TransactionPhase.RollbackFailed;
                }
                else
                {
                    var relationshipAccess = await _relationshipAccessPersistence.GetAccessAsync(
                        assignment.TrainerId,
                        assignment.TraineeId,
                        cancellationToken);
                    var episodeCheck = await StageAssignmentEpisodeRevisionCheckAsync(
                        assignment,
                        relationshipAccess,
                        cancellationToken);
                    if (episodeCheck.IsFailure)
                    {
                        var rollback = await RollbackBusinessDecisionAsync(
                            transaction,
                            assignmentId,
                            AutomaticEligibilityReason.RelationshipUnavailable,
                            cancellationToken);
                        result = rollback.Result;
                        abort = rollback.Failure == null ? null : ExceptionDispatchInfo.Capture(rollback.Failure);
                        phase = rollback.Failure == null
                            ? TransactionPhase.RolledBack
                            : TransactionPhase.RollbackFailed;
                    }
                    else if (assignment.Template.IsDeleted)
                    {
                        var inactive = await InvalidatePendingScheduleStateAsync(
                            assignment,
                            assignment.TrainerId,
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
                            result = ProcessingResult.Skipped(
                                assignmentId,
                                AutomaticEligibilityReason.RelationshipUnavailable);
                        }
                        else
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            phase = TransactionPhase.CommitStarted;
                            await transaction.CommitAsync(cancellationToken);
                            phase = TransactionPhase.Committed;
                            result = ProcessingResult.Deactivated(assignmentId);
                        }
                    }
                    else
                    {
                        var stagedResult = await StageRequestCreationAsync(
                            assignment,
                            relationshipAccess,
                            now,
                            cancellationToken);
                        if (stagedResult.IsFailure)
                        {
                            var rollback = await RollbackBusinessDecisionAsync(
                                transaction,
                                assignmentId,
                                AutomaticEligibilityReason.TemplatePhotoTypeUnavailable,
                                cancellationToken);
                            result = rollback.Result;
                            abort = rollback.Failure == null ? null : ExceptionDispatchInfo.Capture(rollback.Failure);
                            phase = rollback.Failure == null
                                ? TransactionPhase.RolledBack
                                : TransactionPhase.RollbackFailed;
                        }
                        else
                        {
                            var staged = stagedResult.Value;
                            if (staged.EnvelopeId == null)
                            {
                                preCommitReason = ProcessingReason.EnvelopeMissing;
                                throw new InvalidOperationException("The recurring report notification envelope was not staged.");
                            }

                            await _unitOfWork.SaveChangesAsync(cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();
                            phase = TransactionPhase.CommitStarted;
                            await transaction.CommitAsync(cancellationToken);
                            phase = TransactionPhase.Committed;
                            result = CreateProcessingResult(assignmentId, staged.RequestId, staged.EnvelopeId);
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
                result = ProcessingResult.Aborted(
                    assignmentId,
                    ProcessingReason.CancellationRequested,
                    exception.GetType().Name);
                abort = ExceptionDispatchInfo.Capture(exception);
            }
            else
            {
                phase = TransactionPhase.RollbackFailed;
                result = ProcessingResult.Aborted(
                    assignmentId,
                    ProcessingReason.RollbackFailed,
                    rollbackFailure.GetType().Name);
                abort = ExceptionDispatchInfo.Capture(rollbackFailure);
            }
        }
        catch (Exception exception) when (phase == TransactionPhase.PreCommit)
        {
            var rollbackFailure = await TryRollbackAsync(transaction);
            if (rollbackFailure == null)
            {
                phase = TransactionPhase.RolledBack;
                result = ProcessingResult.Failed(assignmentId, preCommitReason, exception.GetType().Name);
            }
            else
            {
                phase = TransactionPhase.RollbackFailed;
                result = ProcessingResult.Aborted(
                    assignmentId,
                    ProcessingReason.RollbackFailed,
                    rollbackFailure.GetType().Name);
                abort = ExceptionDispatchInfo.Capture(rollbackFailure);
            }
        }
        catch (Exception exception) when (phase == TransactionPhase.CommitStarted)
        {
            result = ProcessingResult.Aborted(
                assignmentId,
                ProcessingReason.CommitFailed,
                exception.GetType().Name);
            abort = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            await transaction.DisposeAsync();
        }
        catch (Exception exception)
        {
            LogAborted(assignmentId, ProcessingReason.DisposeFailed, exception.GetType().Name);
            throw;
        }

        LogResult(result!);
        abort?.Throw();
    }

    private async Task<BusinessRollbackResult> RollbackBusinessDecisionAsync(
        IUnitOfWorkTransaction transaction,
        Id<RecurringReportAssignment> assignmentId,
        AutomaticEligibilityReason reason,
        CancellationToken cancellationToken)
    {
        var rollbackFailure = await TryRollbackAsync(transaction);
        if (rollbackFailure == null && cancellationToken.IsCancellationRequested)
        {
            var cancellation = new OperationCanceledException(cancellationToken);
            return new BusinessRollbackResult(
                ProcessingResult.Aborted(
                    assignmentId,
                    ProcessingReason.CancellationRequested,
                    cancellation.GetType().Name),
                cancellation);
        }

        return rollbackFailure == null
            ? new BusinessRollbackResult(ProcessingResult.Skipped(assignmentId, reason), null)
            : new BusinessRollbackResult(
                ProcessingResult.Aborted(
                    assignmentId,
                    ProcessingReason.RollbackFailed,
                    rollbackFailure.GetType().Name),
                rollbackFailure);
    }

    private static async Task<Exception?> TryRollbackAsync(IUnitOfWorkTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private void LogResult(ProcessingResult result)
    {
        switch (result.Outcome)
        {
            case ProcessingOutcome.Created:
                _logger.LogInformation(
                    "Recurring assignment {AssignmentId} produced request {RequestId} and envelope {EnvelopeId}; outcome {Outcome}, reason {Reason}.",
                    result.AssignmentId, result.RequestId, result.EnvelopeId, result.Outcome, result.Reason);
                break;
            case ProcessingOutcome.Deactivated:
                _logger.LogInformation(
                    "Recurring assignment {AssignmentId} processing completed; outcome {Outcome}, reason {Reason}.",
                    result.AssignmentId, result.Outcome, result.Reason);
                break;
            case ProcessingOutcome.Failed:
                _logger.LogWarning(
                    "Recurring assignment {AssignmentId} processing completed; outcome {Outcome}, reason {Reason}, exception type {ExceptionType}.",
                    result.AssignmentId, result.Outcome, result.Reason, result.ExceptionType);
                break;
            case ProcessingOutcome.Aborted:
                LogAborted(result.AssignmentId, (ProcessingReason)result.Reason, result.ExceptionType!);
                break;
            case ProcessingOutcome.Skipped when result.Reason is AutomaticEligibilityReason.NotDue:
                _logger.LogDebug(
                    "Recurring assignment {AssignmentId} processing completed; outcome {Outcome}, reason {Reason}.",
                    result.AssignmentId, result.Outcome, result.Reason);
                break;
            case ProcessingOutcome.Skipped:
                _logger.LogInformation(
                    "Recurring assignment {AssignmentId} processing completed; outcome {Outcome}, reason {Reason}.",
                    result.AssignmentId, result.Outcome, result.Reason);
                break;
        }
    }

    private void LogAborted(
        Id<RecurringReportAssignment> assignmentId,
        ProcessingReason reason,
        string exceptionType)
        => _logger.LogError(
            "Recurring assignment {AssignmentId} processing completed; outcome {Outcome}, reason {Reason}, exception type {ExceptionType}.",
            assignmentId, ProcessingOutcome.Aborted, reason, exceptionType);

}
