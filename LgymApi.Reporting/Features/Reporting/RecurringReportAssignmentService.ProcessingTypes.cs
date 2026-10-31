using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class RecurringReportAssignmentService
{
    private static ProcessingResult CreateProcessingResult(
        Id<RecurringReportAssignment> assignmentId,
        Id<ReportRequest> requestId,
        string envelopeId)
        => ProcessingResult.Created(assignmentId, requestId, envelopeId);

    private sealed record ProcessingResult(
        Id<RecurringReportAssignment> AssignmentId,
        ProcessingOutcome Outcome,
        object Reason,
        Id<ReportRequest>? RequestId = null,
        string? EnvelopeId = null,
        string? ExceptionType = null)
    {
        public static ProcessingResult Created(
            Id<RecurringReportAssignment> id,
            Id<ReportRequest> requestId,
            string envelopeId)
            => new(id, ProcessingOutcome.Created, AutomaticEligibilityReason.Eligible, requestId, envelopeId);

        public static ProcessingResult Deactivated(Id<RecurringReportAssignment> id)
            => new(id, ProcessingOutcome.Deactivated, ProcessingReason.TemplateDeleted);

        public static ProcessingResult Skipped(Id<RecurringReportAssignment> id, AutomaticEligibilityReason reason)
            => new(id, ProcessingOutcome.Skipped, reason);

        public static ProcessingResult Failed(Id<RecurringReportAssignment> id, ProcessingReason reason, string exceptionType)
            => new(id, ProcessingOutcome.Failed, reason, ExceptionType: exceptionType);

        public static ProcessingResult Aborted(Id<RecurringReportAssignment> id, ProcessingReason reason, string exceptionType)
            => new(id, ProcessingOutcome.Aborted, reason, ExceptionType: exceptionType);
    }

    private sealed record StagedRequestCreation(
        Id<ReportRequest> RequestId,
        string? EnvelopeId,
        RecurringReportAssignmentPersistenceModel Assignment);

    private sealed record BusinessRollbackResult(ProcessingResult Result, Exception? Failure);

    private enum TransactionPhase { PreCommit, RolledBack, RollbackFailed, CommitStarted, Committed }
    private enum ProcessingOutcome { Skipped, Created, Deactivated, Failed, Aborted }

    private enum ProcessingReason
    {
        TemplateDeleted,
        EnvelopeMissing,
        PreCommitFailed,
        CancellationRequested,
        BeginTransactionFailed,
        CommitFailed,
        RollbackFailed,
        DisposeFailed
    }
}
