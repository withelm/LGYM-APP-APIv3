using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class RecurringReportAssignmentRepository : IRecurringReportAssignmentRepository
{
    private readonly AppDbContext _dbContext;

    public RecurringReportAssignmentRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(RecurringReportAssignment assignment, CancellationToken cancellationToken = default)
    {
        await _dbContext.RecurringReportAssignments.AddAsync(assignment, cancellationToken);
    }

    public Task<RecurringReportAssignment?> FindByIdForTrainerAsync(Id<RecurringReportAssignment> assignmentId, Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default)
    {
        return BaseQuery()
            .FirstOrDefaultAsync(
                x => x.Id == assignmentId && x.TrainerId == trainerId && x.TraineeId == traineeId,
                cancellationToken);
    }

    public Task<RecurringReportAssignment?> FindByIdAsync(Id<RecurringReportAssignment> assignmentId, CancellationToken cancellationToken = default)
    {
        return BaseQuery().FirstOrDefaultAsync(x => x.Id == assignmentId, cancellationToken);
    }

    public Task<RecurringReportAssignment?> FindByCurrentReportRequestIdAsync(Id<ReportRequest> reportRequestId, CancellationToken cancellationToken = default)
    {
        return BaseQuery().FirstOrDefaultAsync(x => x.CurrentReportRequestId == reportRequestId, cancellationToken);
    }

    public Task<List<RecurringReportAssignment>> GetByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default)
    {
        return BaseQuery()
            .AsNoTracking()
            .Where(x => x.TrainerId == trainerId && x.TraineeId == traineeId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<RecurringReportAssignment>> GetDueAssignmentsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return BaseQuery()
            .Where(x => x.IsActive && x.StartsAt <= now && (!x.EndsAt.HasValue || x.EndsAt >= now))
            .OrderBy(x => x.NextEligibleAt ?? x.StartsAt)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<RecurringReportAssignment> BaseQuery()
    {
        return _dbContext.RecurringReportAssignments
            .Include(x => x.Template)
                .ThenInclude(x => x.Fields.OrderBy(f => f.Order).ThenBy(f => f.CreatedAt))
            .Include(x => x.CurrentReportRequest)
                .ThenInclude(x => x.Template)
                    .ThenInclude(x => x.Fields.OrderBy(f => f.Order).ThenBy(f => f.CreatedAt))
            .Include(x => x.CurrentReportRequest)
                .ThenInclude(x => x.Submission);
    }
}
