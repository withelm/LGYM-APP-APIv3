using System.Globalization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Notifications.Authorization;

internal sealed class NotificationSourceResource;

internal enum NotificationSourceKind
{
    Invitation,
    ReportRequest,
    ReportSubmission,
    DietPlan,
    TraineeNote,
    Relationship,
    RecurringReportAssignment
}

internal enum NotificationSourceState
{
    None,
    Pending,
    Accepted,
    Rejected,
    Submitted,
    Cancelled,
    Expired,
    Completed
}

internal enum NotificationSourceRecipientRole
{
    Trainer,
    Trainee
}

internal sealed record NotificationSourceReference(
    Id<InAppNotification> NotificationId,
    NotificationSourceKind Kind,
    Id<NotificationSourceResource> SourceId,
    Id<User> RecipientId,
    Id<User>? SenderId,
    NotificationSourceRecipientRole RecipientRole,
    NotificationSourceState RequiredState,
    bool RequiresVisibleContent,
    bool RequiresActiveSourceEpisode = false,
    int? ExpectedScheduleRevision = null);

internal sealed record NotificationSourceFacts(
    bool SourceExists,
    bool SourceDeleted,
    bool RecipientAvailable,
    bool SenderAvailable,
    bool ParticipantsMatch,
    NotificationSourceState State,
    bool VisibleToRecipient,
    bool HasExpectedContent,
    bool RelationshipCurrentlyActive,
    bool HasActiveMatchingSourceEpisode = false,
    bool ScheduleRevisionMatches = true)
{
    public static NotificationSourceFacts Missing { get; } = new(
        false,
        false,
        false,
        false,
        false,
        NotificationSourceState.None,
        false,
        false,
        false);
}

internal interface INotificationSourceFactPort
{
    Task<bool> StageReminderFenceAsync(NotificationSourceReference reference, CancellationToken cancellationToken = default);

    Task<bool> LockReminderDeliveryAsync(NotificationSourceReference reference, CancellationToken cancellationToken = default)
        => StageReminderFenceAsync(reference, cancellationToken);

    Task<IReadOnlyDictionary<Id<InAppNotification>, NotificationSourceFacts>> GetFactsAsync(
        IReadOnlyCollection<NotificationSourceReference> references,
        CancellationToken cancellationToken = default);
}

internal static class NotificationSourceEligibilityPolicy
{
    public static bool CanDisclose(NotificationSourceReference source, NotificationSourceFacts facts)
    {
        if (!facts.SourceExists
            || facts.SourceDeleted
            || !facts.RecipientAvailable
            || !facts.SenderAvailable
            || !facts.ParticipantsMatch)
        {
            return false;
        }

        return source.Kind switch
        {
            NotificationSourceKind.Invitation => facts.State == source.RequiredState,
            NotificationSourceKind.ReportRequest => facts.State == source.RequiredState
                && (!source.RequiresActiveSourceEpisode || facts.HasActiveMatchingSourceEpisode),
            NotificationSourceKind.ReportSubmission => (facts.State == source.RequiredState
                    || (source.RequiredState == NotificationSourceState.Submitted && facts.State == NotificationSourceState.Completed))
                && (!source.RequiresVisibleContent || facts.HasExpectedContent)
                && (!source.RequiresActiveSourceEpisode || facts.HasActiveMatchingSourceEpisode),
            NotificationSourceKind.DietPlan => true,
            NotificationSourceKind.TraineeNote => facts.VisibleToRecipient,
            NotificationSourceKind.Relationship => !facts.RelationshipCurrentlyActive,
            NotificationSourceKind.RecurringReportAssignment => facts.HasActiveMatchingSourceEpisode
                && facts.ScheduleRevisionMatches,
            _ => false
        };
    }
}

internal static class NotificationSourceReferenceParser
{
    public static bool TryParse(InAppNotification notification, out NotificationSourceReference source)
    {
        source = null!;
        if (notification.SenderUserId is not { } senderId || string.IsNullOrWhiteSpace(notification.DeliveryKey))
        {
            return false;
        }

        var type = notification.Type;
        if (type == InAppNotificationTypes.InvitationSent)
        {
            return TryCreate(notification, "trainer-invitation:", ":sent", NotificationSourceKind.Invitation,
                NotificationSourceRecipientRole.Trainee, NotificationSourceState.Pending, false, out source);
        }

        if (type == InAppNotificationTypes.InvitationAccepted)
        {
            return TryCreate(notification, "trainer-invitation:", ":accepted", NotificationSourceKind.Invitation,
                NotificationSourceRecipientRole.Trainer, NotificationSourceState.Accepted, false, out source);
        }

        if (type == InAppNotificationTypes.InvitationRejected)
        {
            return TryCreate(notification, "trainer-invitation:", ":rejected", NotificationSourceKind.Invitation,
                NotificationSourceRecipientRole.Trainer, NotificationSourceState.Rejected, false, out source);
        }

        if (type == InAppNotificationTypes.ReportRequestReceived)
        {
            if (TryCreate(notification, "report-request:", ":created", NotificationSourceKind.ReportRequest,
                    NotificationSourceRecipientRole.Trainee, NotificationSourceState.Pending, false, out source))
            {
                return true;
            }

            return TryCreateRecurringReminder(notification, out source);
        }

        if (type == InAppNotificationTypes.ReportSubmissionReceived)
        {
            return TryCreate(notification, "report-submission:", null, NotificationSourceKind.ReportSubmission,
                NotificationSourceRecipientRole.Trainer, NotificationSourceState.Submitted, false, out source);
        }

        if (type == InAppNotificationTypes.ReportFeedbackReceived)
        {
            return TryCreate(notification, "report-feedback:", ":", NotificationSourceKind.ReportSubmission,
                NotificationSourceRecipientRole.Trainee, NotificationSourceState.Submitted, true, out source);
        }

        if (type == InAppNotificationTypes.ReportFeedbackRead)
        {
            var parsed = TryCreate(notification, "report-feedback-read:", null, NotificationSourceKind.ReportSubmission,
                NotificationSourceRecipientRole.Trainer, NotificationSourceState.Completed, false, out source);
            if (parsed)
            {
                source = source with { RequiresActiveSourceEpisode = true };
            }
            return parsed;
        }

        if (type == InAppNotificationTypes.DietPlanUpdated)
        {
            return TryCreate(notification, "diet-plan:", ":", NotificationSourceKind.DietPlan,
                NotificationSourceRecipientRole.Trainee, NotificationSourceState.None, false, out source);
        }

        if (type == InAppNotificationTypes.TraineeNoteUpdated)
        {
            return TryCreate(notification, "trainee-note:", ":", NotificationSourceKind.TraineeNote,
                NotificationSourceRecipientRole.Trainee, NotificationSourceState.None, false, out source);
        }

        if (type == InAppNotificationTypes.TrainerRelationshipEnded)
        {
            var expectedKey = $"trainer-relationship-ended:{notification.RecipientId}:{senderId}";
            if (!string.Equals(notification.DeliveryKey, expectedKey, StringComparison.Ordinal))
            {
                return false;
            }

            source = new NotificationSourceReference(
                notification.Id,
                NotificationSourceKind.Relationship,
                senderId.Rebind<NotificationSourceResource>(),
                notification.RecipientId,
                senderId,
                NotificationSourceRecipientRole.Trainer,
                NotificationSourceState.None,
                false);
            return true;
        }

        return false;
    }

    private static bool TryCreateRecurringReminder(InAppNotification notification, out NotificationSourceReference source)
    {
        source = null!;
        const string prefix = "report-recurring-reminder:";
        var deliveryKey = notification.DeliveryKey!;
        if (!deliveryKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = deliveryKey[prefix.Length..].Split(':');
        if (parts is ["upcoming", var assignmentId, var revision]
            && Id<NotificationSourceResource>.TryParse(assignmentId, out var assignment)
            && int.TryParse(revision, CultureInfo.InvariantCulture, out var expectedRevision)
            && expectedRevision >= 0)
        {
            source = new NotificationSourceReference(
                notification.Id,
                NotificationSourceKind.RecurringReportAssignment,
                assignment,
                notification.RecipientId,
                notification.SenderUserId,
                NotificationSourceRecipientRole.Trainee,
                NotificationSourceState.None,
                false,
                RequiresActiveSourceEpisode: true,
                ExpectedScheduleRevision: expectedRevision);
            return true;
        }

        if (parts is ["unanswered", var requestId]
            && Id<NotificationSourceResource>.TryParse(requestId, out var request))
        {
            source = new NotificationSourceReference(
                notification.Id,
                NotificationSourceKind.ReportRequest,
                request,
                notification.RecipientId,
                notification.SenderUserId,
                NotificationSourceRecipientRole.Trainee,
                NotificationSourceState.Pending,
                false,
                RequiresActiveSourceEpisode: true);
            return true;
        }

        return false;
    }

    private static bool TryCreate(
        InAppNotification notification,
        string prefix,
        string? suffixOrSeparator,
        NotificationSourceKind kind,
        NotificationSourceRecipientRole recipientRole,
        NotificationSourceState requiredState,
        bool requiresVisibleContent,
        out NotificationSourceReference source)
    {
        source = null!;
        var deliveryKey = notification.DeliveryKey!;
        if (!deliveryKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var remainder = deliveryKey[prefix.Length..];
        string sourceId;
        if (suffixOrSeparator == null)
        {
            sourceId = remainder;
        }
        else if (suffixOrSeparator == ":")
        {
            var separatorIndex = remainder.IndexOf(':');
            if (separatorIndex <= 0)
            {
                return false;
            }

            sourceId = remainder[..separatorIndex];
            var version = remainder[(separatorIndex + 1)..];
            if (!DateTimeOffset.TryParseExact(
                    version,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var timestamp)
                || !string.Equals(version, timestamp.ToString("O", CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                return false;
            }
        }
        else
        {
            if (!remainder.EndsWith(suffixOrSeparator, StringComparison.Ordinal))
            {
                return false;
            }

            sourceId = remainder[..^suffixOrSeparator.Length];
        }

        if (!Id<NotificationSourceResource>.TryParse(sourceId, out var parsedSourceId))
        {
            return false;
        }

        source = new NotificationSourceReference(
            notification.Id,
            kind,
            parsedSourceId,
            notification.RecipientId,
            notification.SenderUserId,
            recipientRole,
            requiredState,
            requiresVisibleContent);
        return true;
    }
}
