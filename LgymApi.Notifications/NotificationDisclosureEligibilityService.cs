using LgymApi.Application.Notifications.Authorization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Notifications;

internal interface INotificationDisclosureEligibilityService
{
    Task<bool> CanDiscloseAsync(
        Id<InAppNotification> notificationId,
        CancellationToken cancellationToken = default);

    Task<(bool Available, NotificationSourceReference? Reminder)> GetReminderReferenceAsync(Id<InAppNotification> notificationId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Reminder source lookup is not supported by this eligibility service.");
    Task<bool> LockReminderDeliveryAsync(NotificationSourceReference reference, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Reminder delivery locking is not supported by this eligibility service.");
}

internal sealed class NotificationDisclosureEligibilityService(
    IInAppNotificationRepository notifications,
    INotificationSourceFactPort sourceFacts) : INotificationDisclosureEligibilityService
{
    public async Task<(bool Available, NotificationSourceReference? Reminder)> GetReminderReferenceAsync(Id<InAppNotification> notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await notifications.GetByIdAsync(notificationId, cancellationToken);
        if (notification == null) return (false, null);
        if (notification.DeliveryKey?.StartsWith("report-recurring-reminder:", StringComparison.Ordinal) != true)
            return (true, null);
        return NotificationSourceReferenceParser.TryParse(notification, out var source)
            ? (true, source) : (false, null);
    }

    public Task<bool> LockReminderDeliveryAsync(NotificationSourceReference reference, CancellationToken cancellationToken = default)
        => sourceFacts.LockReminderDeliveryAsync(reference, cancellationToken);

    internal async Task<bool> StageReminderFenceAsync(InAppNotification notification, CancellationToken cancellationToken)
    {
        if (!NotificationSourceReferenceParser.TryParse(notification, out var source)
            || !source.RequiresActiveSourceEpisode
            || source.Kind is not (NotificationSourceKind.ReportRequest or NotificationSourceKind.RecurringReportAssignment))
        {
            return false;
        }

        return await sourceFacts.StageReminderFenceAsync(source, cancellationToken);
    }

    public async Task<bool> CanDiscloseAsync(
        Id<InAppNotification> notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await notifications.GetByIdAsync(notificationId, cancellationToken);
        return notification != null
            && await CanDiscloseAsync(notification, cancellationToken);
    }

    internal async Task<bool> CanDiscloseAsync(
        InAppNotification notification,
        CancellationToken cancellationToken = default)
    {
        var ids = await GetDisclosableIdsAsync([notification], cancellationToken);
        return ids.Contains(notification.Id);
    }

    internal async Task<IReadOnlySet<Id<InAppNotification>>> GetDisclosableIdsAsync(
        IReadOnlyCollection<InAppNotification> candidates,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
        {
            return new HashSet<Id<InAppNotification>>();
        }

        var allowed = candidates
            .Where(notification => notification.IsSystemNotification
                && string.IsNullOrWhiteSpace(notification.DeliveryKey))
            .Select(notification => notification.Id)
            .ToHashSet();
        var references = new List<NotificationSourceReference>();

        foreach (var notification in candidates.Where(candidate => !allowed.Contains(candidate.Id)))
        {
            if (NotificationSourceReferenceParser.TryParse(notification, out var source))
            {
                references.Add(source);
            }
        }

        var facts = await sourceFacts.GetFactsAsync(references, cancellationToken);
        foreach (var source in references)
        {
            if (facts.TryGetValue(source.NotificationId, out var sourceFact)
                && NotificationSourceEligibilityPolicy.CanDisclose(source, sourceFact))
            {
                allowed.Add(source.NotificationId);
            }
        }

        return allowed;
    }
}
