using LgymApi.Application.Notifications.Authorization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories.Notifications;

internal sealed class NotificationSourceFactProvider(AppDbContext database) : INotificationSourceFactPort
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

        var submissionIds = SourceIds<ReportSubmission>(references, NotificationSourceKind.ReportSubmission);
        var submissions = await database.ReportSubmissions.IgnoreQueryFilters().AsNoTracking()
            .Where(submission => submissionIds.Contains(submission.Id))
            .ToDictionaryAsync(submission => submission.Id, cancellationToken);
        var submissionRequestIds = submissions.Values.Select(submission => submission.ReportRequestId).Distinct().ToArray();
        var submissionRequests = await database.ReportRequests.IgnoreQueryFilters().AsNoTracking()
            .Where(request => submissionRequestIds.Contains(request.Id))
            .ToDictionaryAsync(request => request.Id, cancellationToken);

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
                NotificationSourceKind.ReportRequest => ReportRequestFacts(reference, directRequests, recipientAvailable, senderAvailable),
                NotificationSourceKind.ReportSubmission => ReportSubmissionFacts(reference, submissions, submissionRequests, recipientAvailable, senderAvailable),
                NotificationSourceKind.DietPlan => DietFacts(reference, diets, recipientAvailable, senderAvailable),
                NotificationSourceKind.TraineeNote => NoteFacts(reference, notes, recipientAvailable, senderAvailable),
                NotificationSourceKind.Relationship => RelationshipFacts(reference, activeRelationships, recipientAvailable, senderAvailable),
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

    private static NotificationSourceFacts InvitationFacts(
        NotificationSourceReference reference,
        IReadOnlyDictionary<Id<TrainerInvitation>, TrainerInvitation> invitations,
        bool recipientAvailable,
        bool senderAvailable)
    {
        if (!invitations.TryGetValue(reference.SourceId.Rebind<TrainerInvitation>(), out var invitation))
        {
            return NotificationSourceFacts.Missing;
        }

        var participantsMatch = reference.RecipientRole == NotificationSourceRecipientRole.Trainee
            ? invitation.TraineeId == reference.RecipientId && invitation.TrainerId == reference.SenderId
            : invitation.TrainerId == reference.RecipientId && invitation.TraineeId == reference.SenderId;
        return Facts(invitation.IsDeleted, recipientAvailable, senderAvailable, participantsMatch, invitation.Status switch
        {
            TrainerInvitationStatus.Pending => NotificationSourceState.Pending,
            TrainerInvitationStatus.Accepted => NotificationSourceState.Accepted,
            TrainerInvitationStatus.Rejected => NotificationSourceState.Rejected,
            TrainerInvitationStatus.Expired or TrainerInvitationStatus.Revoked => NotificationSourceState.Expired,
            _ => NotificationSourceState.None
        });
    }

    private static NotificationSourceFacts ReportRequestFacts(
        NotificationSourceReference reference,
        IReadOnlyDictionary<Id<ReportRequest>, ReportRequest> requests,
        bool recipientAvailable,
        bool senderAvailable)
    {
        if (!requests.TryGetValue(reference.SourceId.Rebind<ReportRequest>(), out var request))
        {
            return NotificationSourceFacts.Missing;
        }

        return Facts(
            request.IsDeleted,
            recipientAvailable,
            senderAvailable,
            request.TraineeId == reference.RecipientId && request.TrainerId == reference.SenderId,
            ReportState(request.Status));
    }

    private static NotificationSourceFacts ReportSubmissionFacts(
        NotificationSourceReference reference,
        IReadOnlyDictionary<Id<ReportSubmission>, ReportSubmission> submissions,
        IReadOnlyDictionary<Id<ReportRequest>, ReportRequest> requests,
        bool recipientAvailable,
        bool senderAvailable)
    {
        if (!submissions.TryGetValue(reference.SourceId.Rebind<ReportSubmission>(), out var submission)
            || !requests.TryGetValue(submission.ReportRequestId, out var request))
        {
            return NotificationSourceFacts.Missing;
        }

        var participantsMatch = reference.RecipientRole == NotificationSourceRecipientRole.Trainer
            ? request.TrainerId == reference.RecipientId && request.TraineeId == reference.SenderId
            : request.TraineeId == reference.RecipientId && request.TrainerId == reference.SenderId;
        participantsMatch = participantsMatch && submission.TraineeId == request.TraineeId;
        return Facts(
            submission.IsDeleted || request.IsDeleted,
            recipientAvailable,
            senderAvailable,
            participantsMatch,
            ReportState(request.Status),
            hasExpectedContent: submission.TrainerFeedbackAddedAt.HasValue);
    }

    private static NotificationSourceFacts DietFacts(
        NotificationSourceReference reference,
        IReadOnlyDictionary<Id<DietPlan>, DietPlan> diets,
        bool recipientAvailable,
        bool senderAvailable)
    {
        if (!diets.TryGetValue(reference.SourceId.Rebind<DietPlan>(), out var diet))
        {
            return NotificationSourceFacts.Missing;
        }

        return Facts(
            diet.IsDeleted,
            recipientAvailable,
            senderAvailable,
            diet.TraineeId == reference.RecipientId && diet.TrainerId == reference.SenderId);
    }

    private static NotificationSourceFacts NoteFacts(
        NotificationSourceReference reference,
        IReadOnlyDictionary<Id<TraineeNote>, TraineeNote> notes,
        bool recipientAvailable,
        bool senderAvailable)
    {
        if (!notes.TryGetValue(reference.SourceId.Rebind<TraineeNote>(), out var note))
        {
            return NotificationSourceFacts.Missing;
        }

        return Facts(
            note.IsDeleted,
            recipientAvailable,
            senderAvailable,
            note.TraineeId == reference.RecipientId && note.TrainerId == reference.SenderId,
            visibleToRecipient: note.VisibleToTrainee);
    }

    private static NotificationSourceFacts RelationshipFacts(
        NotificationSourceReference reference,
        IReadOnlyCollection<RelationshipRow> activeRelationships,
        bool recipientAvailable,
        bool senderAvailable)
    {
        var relationshipActive = activeRelationships.Any(link =>
            link.TrainerId == reference.RecipientId && link.TraineeId == reference.SenderId);
        return Facts(
            false,
            recipientAvailable,
            senderAvailable,
            reference.SenderId.HasValue,
            relationshipCurrentlyActive: relationshipActive);
    }

    private static NotificationSourceFacts Facts(
        bool sourceDeleted,
        bool recipientAvailable,
        bool senderAvailable,
        bool participantsMatch,
        NotificationSourceState state = NotificationSourceState.None,
        bool visibleToRecipient = true,
        bool hasExpectedContent = true,
        bool relationshipCurrentlyActive = false)
        => new(
            true,
            sourceDeleted,
            recipientAvailable,
            senderAvailable,
            participantsMatch,
            state,
            visibleToRecipient,
            hasExpectedContent,
            relationshipCurrentlyActive);

    private static NotificationSourceState ReportState(ReportRequestStatus status) => status switch
    {
        ReportRequestStatus.Pending => NotificationSourceState.Pending,
        ReportRequestStatus.Submitted => NotificationSourceState.Submitted,
        ReportRequestStatus.Cancelled => NotificationSourceState.Cancelled,
        ReportRequestStatus.Expired => NotificationSourceState.Expired,
        _ => NotificationSourceState.None
    };

    private sealed record AccountAvailabilityRow(Id<User> Id, bool IsDeleted, bool IsBlocked);

    private sealed record RelationshipRow(Id<User> TrainerId, Id<User> TraineeId);
}
