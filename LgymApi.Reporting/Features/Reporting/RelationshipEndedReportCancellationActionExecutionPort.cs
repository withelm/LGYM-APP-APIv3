using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Reporting;

internal sealed class RelationshipEndedReportCancellationActionExecutionPort(
    IRecurringReportAssignmentPersistence assignmentPersistence,
    IReportRequestSubmissionPersistence requestPersistence,
    IUnitOfWork unitOfWork) : IRelationshipEndedReportCancellationActionExecutionPort
{
    public async Task ExecuteAsync(
        Id<CoachingEpisode> episodeId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var assignments = await assignmentPersistence.ListActiveBySourceEpisodeAsync(episodeId, cancellationToken);
        foreach (var assignment in assignments)
        {
            var locked = await assignmentPersistence.FindByIdForUpdateAsync(assignment.Id, cancellationToken);
            if (locked is null || !locked.IsActive || locked.SourceCoachingEpisodeId != episodeId)
            {
                continue;
            }

            await assignmentPersistence.UpdateAsync(
                locked.Id,
                new RecurringReportAssignmentUpdatePersistenceModel(
                    locked.TemplateId,
                    locked.IntervalValue,
                    locked.IntervalUnit,
                    locked.StartsAt,
                    locked.EndsAt,
                    false,
                    locked.Note,
                    locked.CurrentReportRequestId,
                    locked.LastRequestCreatedAt,
                    null,
                    locked.IsDeleted,
                    locked.ScheduleRevision + 1),
                cancellationToken);
        }

        await requestPersistence.CancelPendingForSourceEpisodeAsync(episodeId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
