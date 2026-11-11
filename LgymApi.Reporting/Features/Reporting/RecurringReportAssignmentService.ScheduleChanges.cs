using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Resources;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class RecurringReportAssignmentService
{
    public async Task<Result<RecurringReportAssignmentResult, AppError>> SetNextOverrideAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<AccountReference> traineeId,
        Id<RecurringReportAssignment> assignmentId,
        SetNextRecurringReportOverrideCommand command,
        CancellationToken cancellationToken = default)
    {
        var ownership = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownership.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(ownership.Error);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var assignmentResult = await GetOwnedAssignmentForUpdateAsync(currentTrainer, traineeId, assignmentId, cancellationToken);
        if (assignmentResult.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(assignmentResult.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var assignment = assignmentResult.Value;
        if (assignment.ScheduleRevision != command.ExpectedScheduleRevision)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(
                new ReportingConflictError(Messages.RecurringReportScheduleRevisionChanged));
        }
        var validation = ValidateScheduleChange(assignment, command.ScheduledAt, now);
        if (validation.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(validation.Error);
        }

        var updated = await SupersedeActiveOverrideAndInvalidateProposalAsync(assignment, currentTrainer.Id, now, cancellationToken);
        updated = updated with { ScheduleRevision = updated.ScheduleRevision + 1, NextEligibleAt = command.ScheduledAt.ToUniversalTime() };
        await _assignmentPersistence.AddScheduleChangeAsync(new NewRecurringReportScheduleChangePersistenceModel(
            Id<RecurringReportScheduleChange>.New(),
            updated.Id,
            RecurringReportScheduleChangeKind.TrainerOverride,
            RecurringReportScheduleChangeStatus.Active,
            updated.ScheduleRevision,
            EffectiveScheduledAt(assignment),
            updated.NextEligibleAt.Value,
            null,
            currentTrainer.Id,
            now), cancellationToken);
        await _assignmentPersistence.UpdateAsync(updated.Id, ToUpdateModel(updated), cancellationToken);
        var saveResult = await SaveEpisodeFencedChangesAsync(transaction, cancellationToken);
        if (saveResult.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(saveResult.Error);
        }
        var reloaded = await _assignmentPersistence.FindByIdAsync(updated.Id, cancellationToken) ?? updated;
        await transaction.CommitAsync(cancellationToken);
        return Result<RecurringReportAssignmentResult, AppError>.Success(MapAssignment(reloaded));
    }

    public async Task<Result<RecurringReportAssignmentResult, AppError>> ProposeNextScheduleAsync(
        AuthenticatedAccountContext currentTrainee,
        Id<RecurringReportAssignment> assignmentId,
        ProposeNextRecurringReportScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (assignmentId.IsEmpty)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var assignment = await _assignmentPersistence.FindByIdForUpdateAsync(assignmentId, cancellationToken);
        if (assignment is null || assignment.TraineeId != currentTrainee.Id)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        var access = await _relationshipAccessPersistence.GetAccessAsync(assignment.TrainerId, currentTrainee.Id, cancellationToken);
        var episodeCheck = await StageAssignmentEpisodeRevisionCheckAsync(assignment, access, cancellationToken);
        if (episodeCheck.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(episodeCheck.Error);
        }

        if (assignment.ScheduleRevision != command.ExpectedScheduleRevision)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(
                new ReportingConflictError(Messages.RecurringReportScheduleRevisionChanged));
        }

        if (command.Reason?.Length > 1000)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(
                new InvalidReportingError(Messages.MaximumLengthExceeded));
        }

        var now = DateTimeOffset.UtcNow;
        var validation = ValidateScheduleChange(assignment, command.ScheduledAt, now);
        if (validation.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(validation.Error);
        }

        if (RecurringReportSchedulePolicy.PendingProposal(assignment) is not null)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(new ReportingConflictError(Messages.RecurringReportProposalPending));
        }

        var normalizedReason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();
        await _assignmentPersistence.AddScheduleChangeAsync(new NewRecurringReportScheduleChangePersistenceModel(
            Id<RecurringReportScheduleChange>.New(),
            assignment.Id,
            RecurringReportScheduleChangeKind.TraineeProposal,
            RecurringReportScheduleChangeStatus.Pending,
            assignment.ScheduleRevision,
            EffectiveScheduledAt(assignment),
            command.ScheduledAt.ToUniversalTime(),
            normalizedReason,
            currentTrainee.Id,
            now), cancellationToken);
        var saveResult = await SaveEpisodeFencedChangesAsync(transaction, cancellationToken);
        if (saveResult.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(saveResult.Error);
        }
        var reloaded = await _assignmentPersistence.FindByIdAsync(assignment.Id, cancellationToken) ?? assignment;
        await transaction.CommitAsync(cancellationToken);
        return Result<RecurringReportAssignmentResult, AppError>.Success(MapAssignment(reloaded));
    }

    public async Task<Result<RecurringReportAssignmentResult, AppError>> ResolveScheduleProposalAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<AccountReference> traineeId,
        Id<RecurringReportAssignment> assignmentId,
        Id<RecurringReportScheduleChange> proposalId,
        bool accept,
        CancellationToken cancellationToken = default)
    {
        var ownership = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownership.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(ownership.Error);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var assignmentResult = await GetOwnedAssignmentForUpdateAsync(currentTrainer, traineeId, assignmentId, cancellationToken);
        if (assignmentResult.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(assignmentResult.Error);
        }

        var assignment = assignmentResult.Value;
        var proposal = (assignment.ScheduleChanges ?? []).SingleOrDefault(change =>
            change.Id == proposalId
            && change.Kind == RecurringReportScheduleChangeKind.TraineeProposal
            && change.Status == RecurringReportScheduleChangeStatus.Pending
            && change.ScheduleRevision == assignment.ScheduleRevision);
        if (proposal is null)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(new ReportingConflictError(Messages.RecurringReportProposalResolved));
        }

        var now = DateTimeOffset.UtcNow;
        if (!accept)
        {
            await _assignmentPersistence.UpdateScheduleChangeResolutionAsync(
                proposal.Id,
                new RecurringReportScheduleChangeResolutionPersistenceModel(
                    RecurringReportScheduleChangeStatus.Rejected,
                    currentTrainer.Id,
                    now),
                cancellationToken);
            var saveResult = await SaveEpisodeFencedChangesAsync(transaction, cancellationToken);
            if (saveResult.IsFailure)
            {
                return Result<RecurringReportAssignmentResult, AppError>.Failure(saveResult.Error);
            }
            var reloaded = await _assignmentPersistence.FindByIdAsync(assignment.Id, cancellationToken) ?? assignment;
            await transaction.CommitAsync(cancellationToken);
            return Result<RecurringReportAssignmentResult, AppError>.Success(MapAssignment(reloaded));
        }

        var validation = ValidateScheduleChange(assignment, proposal.ProposedScheduledAt, now);
        if (validation.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(validation.Error);
        }

        await _assignmentPersistence.UpdateScheduleChangeResolutionAsync(
            proposal.Id,
            new RecurringReportScheduleChangeResolutionPersistenceModel(
                RecurringReportScheduleChangeStatus.Approved,
                currentTrainer.Id,
                now),
            cancellationToken);
        var updated = await SupersedeActiveOverrideAsync(assignment, currentTrainer.Id, now, cancellationToken);
        updated = updated with { ScheduleRevision = updated.ScheduleRevision + 1, NextEligibleAt = proposal.ProposedScheduledAt };
        await _assignmentPersistence.AddScheduleChangeAsync(new NewRecurringReportScheduleChangePersistenceModel(
            Id<RecurringReportScheduleChange>.New(),
            updated.Id,
            RecurringReportScheduleChangeKind.TrainerOverride,
            RecurringReportScheduleChangeStatus.Active,
            updated.ScheduleRevision,
            EffectiveScheduledAt(assignment),
            proposal.ProposedScheduledAt,
            proposal.Reason,
            currentTrainer.Id,
            now,
            SourceProposalId: proposal.Id), cancellationToken);
        await _assignmentPersistence.UpdateAsync(updated.Id, ToUpdateModel(updated), cancellationToken);
        var acceptedSaveResult = await SaveEpisodeFencedChangesAsync(transaction, cancellationToken);
        if (acceptedSaveResult.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(acceptedSaveResult.Error);
        }
        var accepted = await _assignmentPersistence.FindByIdAsync(updated.Id, cancellationToken) ?? updated;
        await transaction.CommitAsync(cancellationToken);
        return Result<RecurringReportAssignmentResult, AppError>.Success(MapAssignment(accepted));
    }

    private static Result<Unit, AppError> ValidateScheduleChange(
        RecurringReportAssignmentPersistenceModel assignment,
        DateTimeOffset scheduledAt,
        DateTimeOffset now)
    {
        var normalized = scheduledAt.ToUniversalTime();
        if (!assignment.IsActive || assignment.IsDeleted || normalized <= now || normalized < assignment.StartsAt
            || (assignment.EndsAt.HasValue && normalized > assignment.EndsAt.Value)
            || HasUnresolvedCurrentRequest(assignment))
        {
            return Result<Unit, AppError>.Failure(new ReportingConflictError(Messages.RecurringReportAssignmentUnavailable));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private static bool HasUnresolvedCurrentRequest(RecurringReportAssignmentPersistenceModel assignment)
    {
        if (!assignment.CurrentReportRequestId.HasValue)
        {
            return false;
        }

        var request = assignment.CurrentReportRequest;
        return request is null
            || request.Status is not (ReportRequestStatus.Submitted or ReportRequestStatus.Completed)
            || request.Submission?.TrainerFeedbackAddedAt is null
            || request.Submission.TrainerFeedbackReadAt is null;
    }

    private async Task<RecurringReportAssignmentPersistenceModel> InvalidatePendingScheduleStateAsync(
        RecurringReportAssignmentPersistenceModel assignment,
        Id<AccountReference> actorId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await SupersedeActiveOverrideAndInvalidateProposalAsync(assignment, actorId, now, cancellationToken);
    }

    private async Task<RecurringReportAssignmentPersistenceModel> SupersedeActiveOverrideAndInvalidateProposalAsync(
        RecurringReportAssignmentPersistenceModel assignment,
        Id<AccountReference> actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var updated = await SupersedeActiveOverrideAsync(assignment, actorId, now, cancellationToken);
        var proposal = RecurringReportSchedulePolicy.PendingProposal(assignment);
        if (proposal is not null)
        {
            await _assignmentPersistence.UpdateScheduleChangeResolutionAsync(
                proposal.Id,
                new RecurringReportScheduleChangeResolutionPersistenceModel(
                    RecurringReportScheduleChangeStatus.Invalidated,
                    actorId,
                    now),
                cancellationToken);
        }

        return updated;
    }

    private async Task<RecurringReportAssignmentPersistenceModel> SupersedeActiveOverrideAsync(
        RecurringReportAssignmentPersistenceModel assignment,
        Id<AccountReference> actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeOverride = RecurringReportSchedulePolicy.ActiveOverride(assignment);
        if (activeOverride is not null)
        {
            await _assignmentPersistence.UpdateScheduleChangeResolutionAsync(
                activeOverride.Id,
                new RecurringReportScheduleChangeResolutionPersistenceModel(
                    RecurringReportScheduleChangeStatus.Superseded,
                    actorId,
                    now),
                cancellationToken);
        }

        return assignment;
    }

    private static DateTimeOffset EffectiveScheduledAt(RecurringReportAssignmentPersistenceModel assignment)
        => RecurringReportSchedulePolicy.EffectiveSchedule(assignment).ScheduledAt ?? assignment.StartsAt;
}
