using LgymApi.Application.Notifications.Authorization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories.Notifications;

internal sealed partial class NotificationSourceFactProvider(AppDbContext database) : INotificationSourceFactPort
{
    public async Task<IReadOnlyDictionary<Id<InAppNotification>, NotificationSourceFacts>> GetFactsAsync(
        IReadOnlyCollection<NotificationSourceReference> references,
        CancellationToken cancellationToken = default)
    {
        if (references.Count == 0)
        {
            return new Dictionary<Id<InAppNotification>, NotificationSourceFacts>();
        }

        var accountIds = references
            .SelectMany(reference => reference.SenderId is { } senderId
                ? new[] { reference.RecipientId, senderId }
                : new[] { reference.RecipientId })
            .Distinct()
            .ToArray();
        var accounts = await database.Users
            .IgnoreQueryFilters()
            .Where(user => accountIds.Contains(user.Id))
            .Select(user => new AccountAvailabilityRow(user.Id, user.IsDeleted, user.IsBlocked))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        var invitationIds = SourceIds<TrainerInvitation>(references, NotificationSourceKind.Invitation);
        var invitations = await database.TrainerInvitations.IgnoreQueryFilters().AsNoTracking()
            .Where(invitation => invitationIds.Contains(invitation.Id))
            .ToDictionaryAsync(invitation => invitation.Id, cancellationToken);

        var requestIds = SourceIds<ReportRequest>(references, NotificationSourceKind.ReportRequest);
        var directRequests = await database.ReportRequests.IgnoreQueryFilters().AsNoTracking()
            .Where(request => requestIds.Contains(request.Id))
            .ToDictionaryAsync(request => request.Id, cancellationToken);

        var assignmentIds = SourceIds<RecurringReportAssignment>(references, NotificationSourceKind.RecurringReportAssignment);
        var assignments = await database.RecurringReportAssignments.IgnoreQueryFilters().AsNoTracking()
            .Where(assignment => assignmentIds.Contains(assignment.Id))
            .ToDictionaryAsync(assignment => assignment.Id, cancellationToken);

        var submissionIds = SourceIds<ReportSubmission>(references, NotificationSourceKind.ReportSubmission);
        var submissions = await database.ReportSubmissions.IgnoreQueryFilters().AsNoTracking()
            .Where(submission => submissionIds.Contains(submission.Id))
            .ToDictionaryAsync(submission => submission.Id, cancellationToken);
        var submissionRequestIds = submissions.Values.Select(submission => submission.ReportRequestId).Distinct().ToArray();
        var submissionRequests = await database.ReportRequests.IgnoreQueryFilters().AsNoTracking()
            .Where(request => submissionRequestIds.Contains(request.Id))
            .ToDictionaryAsync(request => request.Id, cancellationToken);
        var sourceEpisodeIds = submissions.Values
            .Select(submission => submission.SourceCoachingEpisodeId
                ?? (submissionRequests.TryGetValue(submission.ReportRequestId, out var request)
                    ? request.SourceCoachingEpisodeId
                    : null))
            .Concat(directRequests.Values.Select(request => request.SourceCoachingEpisodeId))
            .Concat(assignments.Values.Select(assignment => assignment.SourceCoachingEpisodeId))
            .Where(episodeId => episodeId.HasValue)
            .Select(episodeId => episodeId!.Value)
            .Distinct()
            .ToArray();
        var activeSourceEpisodeIds = sourceEpisodeIds.Length == 0
            ? []
            : await database.TrainerTraineeLinks.IgnoreQueryFilters().AsNoTracking()
                .Where(link => !link.IsDeleted && link.EpisodeId != null)
                .Join(
                    database.CoachingEpisodes.IgnoreQueryFilters().AsNoTracking()
                        .Where(episode => !episode.IsDeleted && episode.EndedAt == null && sourceEpisodeIds.Contains(episode.Id)),
                    link => link.EpisodeId,
                    episode => (Id<CoachingEpisode>?)episode.Id,
                    (_, episode) => episode.Id)
                .ToHashSetAsync(cancellationToken);

        var dietIds = SourceIds<DietPlan>(references, NotificationSourceKind.DietPlan);
        var diets = await database.DietPlans.IgnoreQueryFilters().AsNoTracking()
            .Where(diet => dietIds.Contains(diet.Id))
            .ToDictionaryAsync(diet => diet.Id, cancellationToken);

        var noteIds = SourceIds<TraineeNote>(references, NotificationSourceKind.TraineeNote);
        var notes = await database.TraineeNotes.IgnoreQueryFilters().AsNoTracking()
            .Where(note => noteIds.Contains(note.Id))
            .ToDictionaryAsync(note => note.Id, cancellationToken);

        var relationshipPairs = references
            .Where(reference => reference.Kind == NotificationSourceKind.Relationship && reference.SenderId.HasValue)
            .Select(reference => (TrainerId: reference.RecipientId, TraineeId: reference.SenderId!.Value))
            .Distinct()
            .ToArray();
        var relationshipTrainerIds = relationshipPairs.Select(pair => pair.TrainerId).Distinct().ToArray();
        var relationshipTraineeIds = relationshipPairs.Select(pair => pair.TraineeId).Distinct().ToArray();
        var activeRelationships = await database.TrainerTraineeLinks.IgnoreQueryFilters()
            .Where(link => !link.IsDeleted
                && relationshipTrainerIds.Contains(link.TrainerId)
                && relationshipTraineeIds.Contains(link.TraineeId))
            .Select(link => new RelationshipRow(link.TrainerId, link.TraineeId))
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Id<InAppNotification>, NotificationSourceFacts>();
        foreach (var reference in references)
        {
            var recipientAvailable = IsAvailable(reference.RecipientId, accounts);
            var senderAvailable = reference.SenderId is { } senderId && IsAvailable(senderId, accounts);
            result[reference.NotificationId] = reference.Kind switch
            {
                NotificationSourceKind.Invitation => InvitationFacts(reference, invitations, recipientAvailable, senderAvailable),
                NotificationSourceKind.ReportRequest => ReportRequestFacts(
                    reference,
                    directRequests,
                    activeSourceEpisodeIds,
                    recipientAvailable,
                    senderAvailable),
                NotificationSourceKind.ReportSubmission => ReportSubmissionFacts(
                    reference,
                    submissions,
                    submissionRequests,
                    activeSourceEpisodeIds,
                    recipientAvailable,
                    senderAvailable),
                NotificationSourceKind.DietPlan => DietFacts(reference, diets, recipientAvailable, senderAvailable),
                NotificationSourceKind.TraineeNote => NoteFacts(reference, notes, recipientAvailable, senderAvailable),
                NotificationSourceKind.Relationship => RelationshipFacts(reference, activeRelationships, recipientAvailable, senderAvailable),
                NotificationSourceKind.RecurringReportAssignment => RecurringReportAssignmentFacts(
                    reference,
                    assignments,
                    activeSourceEpisodeIds,
                    recipientAvailable,
                    senderAvailable),
                _ => NotificationSourceFacts.Missing
            };
        }

        return result;
    }

    private static Id<TEntity>[] SourceIds<TEntity>(
        IEnumerable<NotificationSourceReference> references,
        NotificationSourceKind kind)
        where TEntity : EntityBase<TEntity>
        => references.Where(reference => reference.Kind == kind)
            .Select(reference => reference.SourceId.Rebind<TEntity>())
            .Distinct()
            .ToArray();

    private static bool IsAvailable(
        Id<User> accountId,
        IReadOnlyDictionary<Id<User>, AccountAvailabilityRow> accounts)
        => accounts.TryGetValue(accountId, out var account) && !account.IsDeleted && !account.IsBlocked;

    private sealed record AccountAvailabilityRow(Id<User> Id, bool IsDeleted, bool IsBlocked);

    private sealed record RelationshipRow(Id<User> TrainerId, Id<User> TraineeId);
}
