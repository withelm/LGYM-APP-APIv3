using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Application.Reporting.Persistence;

public interface IRecurringReportAssignmentPersistence
{
    Task AddAsync(NewRecurringReportAssignmentPersistenceModel assignment, CancellationToken cancellationToken = default);
    Task<RecurringReportAssignmentPersistenceModel?> FindForTrainerAsync(Id<RecurringReportAssignment> assignmentId, Id<AccountReference> trainerId, Id<AccountReference> traineeId, CancellationToken cancellationToken = default);
    Task<RecurringReportAssignmentPersistenceModel?> FindByIdAsync(Id<RecurringReportAssignment> assignmentId, CancellationToken cancellationToken = default);
    Task<RecurringReportAssignmentPersistenceModel?> FindByIdForUpdateAsync(Id<RecurringReportAssignment> assignmentId, CancellationToken cancellationToken = default);
    Task<RecurringReportAssignmentPersistenceModel?> FindByCurrentRequestAsync(Id<ReportRequest> reportRequestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringReportAssignmentPersistenceModel>> ListByCurrentRequestsAsync(IReadOnlyCollection<Id<ReportRequest>> reportRequestIds, CancellationToken cancellationToken = default);
    Task<RecurringReportAssignmentPersistenceModel?> FindByCurrentRequestForUpdateAsync(Id<ReportRequest> reportRequestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringReportAssignmentPersistenceModel>> ListByTrainerAndTraineeAsync(Id<AccountReference> trainerId, Id<AccountReference> traineeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringReportAssignmentPersistenceModel>> ListByTraineeAsync(Id<AccountReference> traineeId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RecurringReportAssignmentPersistenceModel>>([]);
    Task<IReadOnlyList<RecurringReportAssignmentPersistenceModel>> ListDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringReportAssignmentPersistenceModel>> ListActiveBySourceEpisodeAsync(
        Id<CoachingEpisode> episodeId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RecurringReportAssignmentPersistenceModel>>([]);
    Task AddScheduleChangeAsync(NewRecurringReportScheduleChangePersistenceModel scheduleChange, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    Task UpdateScheduleChangeResolutionAsync(Id<RecurringReportScheduleChange> scheduleChangeId, RecurringReportScheduleChangeResolutionPersistenceModel resolution, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    Task UpdateAsync(Id<RecurringReportAssignment> assignmentId, RecurringReportAssignmentUpdatePersistenceModel update, CancellationToken cancellationToken = default);
}
