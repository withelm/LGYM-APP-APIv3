using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories.Reporting;

public sealed class RecurringReportAssignmentPersistenceRepository : IRecurringReportAssignmentPersistence
{
    private readonly AppDbContext _dbContext;

    public RecurringReportAssignmentPersistenceRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(NewRecurringReportAssignmentPersistenceModel assignment, CancellationToken cancellationToken = default)
        => _dbContext.RecurringReportAssignments.AddAsync(new RecurringReportAssignment
        {
            Id = assignment.Id,
            TrainerId = ReportingPersistenceAccountIds.ToPersisted(assignment.TrainerId),
            TraineeId = ReportingPersistenceAccountIds.ToPersisted(assignment.TraineeId),
            TemplateId = assignment.TemplateId,
            IntervalValue = assignment.IntervalValue,
            IntervalUnit = assignment.IntervalUnit,
            StartsAt = assignment.StartsAt,
            EndsAt = assignment.EndsAt,
            IsActive = assignment.IsActive,
            Note = assignment.Note,
            CurrentReportRequestId = assignment.CurrentReportRequestId,
            LastRequestCreatedAt = assignment.LastRequestCreatedAt,
            NextEligibleAt = assignment.NextEligibleAt,
            ScheduleRevision = assignment.ScheduleRevision,
            SourceCoachingEpisodeId = assignment.SourceCoachingEpisodeId,
            CreatedAt = assignment.CreatedAt
        }, cancellationToken).AsTask();

    public async Task<RecurringReportAssignmentPersistenceModel?> FindForTrainerAsync(
        Id<RecurringReportAssignment> assignmentId,
        Id<AccountReference> trainerId,
        Id<AccountReference> traineeId,
        CancellationToken cancellationToken = default)
    {
        var persistedTrainerId = ReportingPersistenceAccountIds.ToPersisted(trainerId);
        var persistedTraineeId = ReportingPersistenceAccountIds.ToPersisted(traineeId);
        var entity = await BaseQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(assignment =>
                assignment.Id == assignmentId
                && assignment.TrainerId == persistedTrainerId
                && assignment.TraineeId == persistedTraineeId,
                cancellationToken);
        return entity is null ? null : ReportingPersistenceProjection.Assignment(entity);
    }

    public async Task<RecurringReportAssignmentPersistenceModel?> FindByIdAsync(
        Id<RecurringReportAssignment> assignmentId,
        CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(assignment => !assignment.IsDeleted)
            .FirstOrDefaultAsync(assignment => assignment.Id == assignmentId, cancellationToken);
        return entity is null ? null : ReportingPersistenceProjection.Assignment(entity);
    }

    public async Task<RecurringReportAssignmentPersistenceModel?> FindByIdForUpdateAsync(
        Id<RecurringReportAssignment> assignmentId,
        CancellationToken cancellationToken = default)
    {
        RequireActiveTransaction();
        var affectedRows = await _dbContext.RecurringReportAssignments
            .IgnoreQueryFilters()
            .Where(assignment => assignment.Id == assignmentId && !assignment.IsDeleted)
            .StageUpdateAsync(
                _dbContext,
                assignment => assignment.UpdatedAt,
                assignment => assignment.UpdatedAt,
                cancellationToken);
        return affectedRows == 0
            ? null
            : await FindByIdAsync(assignmentId, cancellationToken);
    }

    public async Task<RecurringReportAssignmentPersistenceModel?> FindByCurrentRequestAsync(
        Id<ReportRequest> reportRequestId,
        CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(assignment => assignment.CurrentReportRequestId == reportRequestId, cancellationToken);
        return entity is null ? null : ReportingPersistenceProjection.Assignment(entity);
    }

    public async Task<IReadOnlyList<RecurringReportAssignmentPersistenceModel>> ListByCurrentRequestsAsync(
        IReadOnlyCollection<Id<ReportRequest>> reportRequestIds,
        CancellationToken cancellationToken = default)
    {
        if (reportRequestIds.Count == 0)
        {
            return [];
        }

        var entities = await BaseQuery().AsNoTracking()
            .Where(assignment => assignment.CurrentReportRequestId != null && reportRequestIds.Contains(assignment.CurrentReportRequestId.Value))
            .ToListAsync(cancellationToken);
        return entities.Select(ReportingPersistenceProjection.Assignment).ToList();
    }

    public async Task<RecurringReportAssignmentPersistenceModel?> FindByCurrentRequestForUpdateAsync(
        Id<ReportRequest> reportRequestId,
        CancellationToken cancellationToken = default)
    {
        RequireActiveTransaction();
        var assignmentId = await _dbContext.RecurringReportAssignments
            .AsNoTracking()
            .Where(assignment => !assignment.IsDeleted && assignment.CurrentReportRequestId == reportRequestId)
            .Select(assignment => (Id<RecurringReportAssignment>?)assignment.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return assignmentId is null
            ? null
            : await FindByIdForUpdateAsync(assignmentId.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringReportAssignmentPersistenceModel>> ListByTrainerAndTraineeAsync(
        Id<AccountReference> trainerId,
        Id<AccountReference> traineeId,
        CancellationToken cancellationToken = default)
    {
        var persistedTrainerId = ReportingPersistenceAccountIds.ToPersisted(trainerId);
        var persistedTraineeId = ReportingPersistenceAccountIds.ToPersisted(traineeId);
        var entities = await BaseQuery()
            .AsNoTracking()
            .Where(assignment => assignment.TrainerId == persistedTrainerId && assignment.TraineeId == persistedTraineeId)
            .OrderByDescending(assignment => assignment.CreatedAt)
            .ToListAsync(cancellationToken);
        return entities.Select(ReportingPersistenceProjection.Assignment).ToList();
    }

    public async Task<IReadOnlyList<RecurringReportAssignmentPersistenceModel>> ListByTraineeAsync(
        Id<AccountReference> traineeId,
        CancellationToken cancellationToken = default)
    {
        var persistedTraineeId = ReportingPersistenceAccountIds.ToPersisted(traineeId);
        var entities = await BaseQuery()
            .AsNoTracking()
            .Where(assignment => assignment.TraineeId == persistedTraineeId)
            .OrderByDescending(assignment => assignment.CreatedAt)
            .ToListAsync(cancellationToken);
        return entities.Select(ReportingPersistenceProjection.Assignment).ToList();
    }

    public async Task<IReadOnlyList<RecurringReportAssignmentPersistenceModel>> ListDueAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(assignment => !assignment.IsDeleted && assignment.IsActive)
            .ToListAsync(cancellationToken);
        return entities
            .Select(ReportingPersistenceProjection.Assignment)
            .Where(assignment => assignment.StartsAt <= now.AddHours(24))
            .Where(assignment => !assignment.EndsAt.HasValue || assignment.EndsAt.Value >= now
                || assignment.CurrentReportRequest?.Status == ReportRequestStatus.Pending)
            .OrderBy(assignment => assignment.NextEligibleAt ?? assignment.StartsAt)
            .ThenBy(assignment => assignment.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<RecurringReportAssignmentPersistenceModel>> ListActiveBySourceEpisodeAsync(
        Id<CoachingEpisode> episodeId,
        CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery()
            .AsNoTracking()
            .Where(assignment => !assignment.IsDeleted
                && assignment.IsActive
                && assignment.SourceCoachingEpisodeId == episodeId)
            .ToListAsync(cancellationToken);
        return entities.Select(ReportingPersistenceProjection.Assignment).ToList();
    }

    public async Task UpdateAsync(
        Id<RecurringReportAssignment> assignmentId,
        RecurringReportAssignmentUpdatePersistenceModel update,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RecurringReportAssignments
            .IgnoreQueryFilters()
            .FirstAsync(assignment => assignment.Id == assignmentId, cancellationToken);
        entity.TemplateId = update.TemplateId;
        entity.IntervalValue = update.IntervalValue;
        entity.IntervalUnit = update.IntervalUnit;
        entity.StartsAt = update.StartsAt;
        entity.EndsAt = update.EndsAt;
        entity.IsActive = update.IsActive;
        entity.Note = update.Note;
        entity.CurrentReportRequestId = update.CurrentReportRequestId;
        entity.LastRequestCreatedAt = update.LastRequestCreatedAt;
        entity.NextEligibleAt = update.NextEligibleAt;
        entity.ScheduleRevision = update.ScheduleRevision;
        entity.IsDeleted = update.IsDeleted;
    }

    public Task AddScheduleChangeAsync(
        NewRecurringReportScheduleChangePersistenceModel scheduleChange,
        CancellationToken cancellationToken = default)
        => _dbContext.RecurringReportScheduleChanges.AddAsync(new RecurringReportScheduleChange
        {
            Id = scheduleChange.Id,
            RecurringReportAssignmentId = scheduleChange.AssignmentId,
            Kind = scheduleChange.Kind,
            Status = scheduleChange.Status,
            ScheduleRevision = scheduleChange.ScheduleRevision,
            OldScheduledAt = scheduleChange.OldScheduledAt,
            ProposedScheduledAt = scheduleChange.ProposedScheduledAt,
            Reason = scheduleChange.Reason,
            RequestedByUserId = ReportingPersistenceAccountIds.ToPersisted(scheduleChange.RequestedByAccountId),
            RequestedAt = scheduleChange.RequestedAt,
            ResolvedByUserId = scheduleChange.ResolvedByAccountId is null
                ? null
                : ReportingPersistenceAccountIds.ToPersisted(scheduleChange.ResolvedByAccountId.Value),
            ResolvedAt = scheduleChange.ResolvedAt,
            SourceProposalId = scheduleChange.SourceProposalId,
            CreatedAt = scheduleChange.RequestedAt
        }, cancellationToken).AsTask();

    public async Task UpdateScheduleChangeResolutionAsync(
        Id<RecurringReportScheduleChange> scheduleChangeId,
        RecurringReportScheduleChangeResolutionPersistenceModel resolution,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RecurringReportScheduleChanges
            .FirstAsync(change => change.Id == scheduleChangeId, cancellationToken);
        entity.Status = resolution.Status;
        entity.ResolvedByUserId = resolution.ResolvedByAccountId is null
            ? null
            : ReportingPersistenceAccountIds.ToPersisted(resolution.ResolvedByAccountId.Value);
        entity.ResolvedAt = resolution.ResolvedAt;
    }

    private IQueryable<RecurringReportAssignment> BaseQuery()
        => _dbContext.RecurringReportAssignments
            .AsSplitQuery()
            .Include(assignment => assignment.Template)
                .ThenInclude(template => template.Fields)
            .Include(assignment => assignment.CurrentReportRequest)
                .ThenInclude(request => request!.Template)
                    .ThenInclude(template => template.Fields)
            .Include(assignment => assignment.CurrentReportRequest)
                .ThenInclude(request => request!.Submission)
            .Include(assignment => assignment.ScheduleChanges);

    private void RequireActiveTransaction()
    {
        if (_dbContext.Database.IsRelational() && _dbContext.Database.CurrentTransaction == null)
        {
            throw new InvalidOperationException("Recurring report assignment locking requires an active transaction.");
        }
    }
}
