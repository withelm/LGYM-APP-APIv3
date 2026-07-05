using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface IRecurringReportAssignmentRepository
{
    Task AddAsync(RecurringReportAssignment assignment, CancellationToken cancellationToken = default);
    Task<RecurringReportAssignment?> FindByIdForTrainerAsync(Id<RecurringReportAssignment> assignmentId, Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<RecurringReportAssignment?> FindByIdAsync(Id<RecurringReportAssignment> assignmentId, CancellationToken cancellationToken = default);
    Task<RecurringReportAssignment?> FindByCurrentReportRequestIdAsync(Id<ReportRequest> reportRequestId, CancellationToken cancellationToken = default);
    Task<List<RecurringReportAssignment>> GetByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<List<RecurringReportAssignment>> GetDueAssignmentsAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
