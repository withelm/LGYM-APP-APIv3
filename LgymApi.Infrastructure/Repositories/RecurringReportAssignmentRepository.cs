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

    public async Task<RecurringReportAssignment?> FindByIdForTrainerAsync(Id<RecurringReportAssignment> assignmentId, Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default)
    {
        var assignment = await BaseQuery()
            .FirstOrDefaultAsync(
                x => x.Id == assignmentId && x.TrainerId == trainerId && x.TraineeId == traineeId,
                cancellationToken);

        return assignment is null ? null : SortIncludedFields(assignment);
    }

    public async Task<RecurringReportAssignment?> FindByIdAsync(Id<RecurringReportAssignment> assignmentId, CancellationToken cancellationToken = default)
    {
        var assignment = await BaseQuery().FirstOrDefaultAsync(x => x.Id == assignmentId, cancellationToken);
        return assignment is null ? null : SortIncludedFields(assignment);
    }

    public async Task<RecurringReportAssignment?> FindByCurrentReportRequestIdAsync(Id<ReportRequest> reportRequestId, CancellationToken cancellationToken = default)
    {
        var assignment = await BaseQuery().FirstOrDefaultAsync(x => x.CurrentReportRequestId == reportRequestId, cancellationToken);
        return assignment is null ? null : SortIncludedFields(assignment);
    }

    public async Task<List<RecurringReportAssignment>> GetByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default)
    {
        var assignments = await BaseQuery()
            .AsNoTracking()
            .Where(x => x.TrainerId == trainerId && x.TraineeId == traineeId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return assignments.Select(SortIncludedFields).ToList();
    }

    public async Task<List<RecurringReportAssignment>> GetDueAssignmentsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var activeAssignments = await BaseQuery()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        return activeAssignments
            .Select(SortIncludedFields)
            .Where(x => x.StartsAt <= now)
            .Where(x => !x.EndsAt.HasValue || x.EndsAt.Value >= now)
            .OrderBy(x => x.NextEligibleAt ?? x.StartsAt)
            .ThenBy(x => x.CreatedAt)
            .ToList();
    }

    private IQueryable<RecurringReportAssignment> BaseQuery()
    {
        return _dbContext.RecurringReportAssignments
            .Include(x => x.Template)
                .ThenInclude(x => x.Fields)
            .Include(x => x.CurrentReportRequest)
                .ThenInclude(x => x.Template)
                    .ThenInclude(x => x.Fields)
            .Include(x => x.CurrentReportRequest)
                .ThenInclude(x => x.Submission);
    }

    private static RecurringReportAssignment SortIncludedFields(RecurringReportAssignment assignment)
    {
        SortTemplateFields(assignment.Template);
        SortTemplateFields(assignment.CurrentReportRequest?.Template);
        return assignment;
    }

    private static void SortTemplateFields(ReportTemplate? template)
    {
        if (template == null)
        {
            return;
        }

        template.Fields = template.Fields
            .OrderBy(f => f.Order)
            .ThenBy(f => f.CreatedAt)
            .ToList();
    }
}
