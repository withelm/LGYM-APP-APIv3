using LgymApi.Application.Notifications.Authorization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories.Notifications;

internal sealed partial class NotificationSourceFactProvider
{
    public async Task<bool> LockReminderDeliveryAsync(NotificationSourceReference reference, CancellationToken cancellationToken = default)
    {
        Id<CoachingEpisode>? episodeId;
        if (reference.Kind == NotificationSourceKind.RecurringReportAssignment)
        {
            var id = reference.SourceId.Rebind<RecurringReportAssignment>();
            var count = await database.RecurringReportAssignments.IgnoreQueryFilters()
                .Where(item => item.Id == id && !item.IsDeleted)
                .StageUpdateAsync(database, item => item.UpdatedAt, item => item.UpdatedAt, cancellationToken);
            if (count == 0) return false;
            episodeId = await database.RecurringReportAssignments.AsNoTracking()
                .Where(item => item.Id == id).Select(item => item.SourceCoachingEpisodeId)
                .SingleOrDefaultAsync(cancellationToken);
        }
        else
        {
            var id = reference.SourceId.Rebind<ReportRequest>();
            var count = await database.ReportRequests.IgnoreQueryFilters()
                .Where(item => item.Id == id && !item.IsDeleted)
                .StageUpdateAsync(database, item => item.UpdatedAt, item => item.UpdatedAt, cancellationToken);
            if (count == 0) return false;
            episodeId = await database.ReportRequests.AsNoTracking()
                .Where(item => item.Id == id).Select(item => item.SourceCoachingEpisodeId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (episodeId is null) return false;
        var locked = await database.CoachingEpisodes.IgnoreQueryFilters()
            .Where(item => item.Id == episodeId && !item.IsDeleted && item.EndedAt == null
                && item.TrainerId == reference.SenderId && item.TraineeId == reference.RecipientId)
            .StageUpdateAsync(database, item => item.AuthorizationRevision, item => item.AuthorizationRevision,
                item => item.UpdatedAt, item => item.UpdatedAt, cancellationToken);
        if (locked == 0) return false;
        var facts = await GetFactsAsync([reference], cancellationToken);
        return facts.TryGetValue(reference.NotificationId, out var fact)
            && NotificationSourceEligibilityPolicy.CanDisclose(reference, fact);
    }

    public async Task<bool> StageReminderFenceAsync(NotificationSourceReference reference, CancellationToken cancellationToken = default)
    {
        Id<CoachingEpisode>? episodeId;
        if (reference.Kind == NotificationSourceKind.RecurringReportAssignment)
        {
            var id = reference.SourceId.Rebind<RecurringReportAssignment>();
            var locked = await database.RecurringReportAssignments.IgnoreQueryFilters()
                .Where(item => item.Id == id && !item.IsDeleted)
                .StageUpdateAsync(database, item => item.UpdatedAt, item => item.UpdatedAt, cancellationToken);
            if (locked == 0) return false;
            episodeId = await database.RecurringReportAssignments.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => item.SourceCoachingEpisodeId)
                .SingleOrDefaultAsync(cancellationToken);
        }
        else
        {
            var id = reference.SourceId.Rebind<ReportRequest>();
            var locked = await database.ReportRequests.IgnoreQueryFilters()
                .Where(item => item.Id == id && !item.IsDeleted)
                .StageUpdateAsync(database, item => item.UpdatedAt, item => item.UpdatedAt, cancellationToken);
            if (locked == 0) return false;
            episodeId = await database.ReportRequests.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => item.SourceCoachingEpisodeId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (episodeId is null) return false;
        var facts = await GetFactsAsync([reference], cancellationToken);
        if (!facts.TryGetValue(reference.NotificationId, out var fact)
            || !NotificationSourceEligibilityPolicy.CanDisclose(reference, fact)) return false;

        var episode = await database.CoachingEpisodes.FirstOrDefaultAsync(item =>
            item.Id == episodeId && item.EndedAt == null && !item.IsDeleted
            && item.TrainerId == reference.SenderId && item.TraineeId == reference.RecipientId,
            cancellationToken);
        if (episode is null) return false;
        database.Entry(episode).Property(item => item.AuthorizationRevision).IsModified = true;
        return true;
    }
}
