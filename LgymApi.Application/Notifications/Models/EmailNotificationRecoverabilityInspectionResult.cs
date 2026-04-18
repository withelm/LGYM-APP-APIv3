using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Notifications.Models;

public sealed record EmailNotificationRecoverabilityInspectionResult(
    int BrokenJobsFound,
    int RecoverableNotifications,
    int ActiveJobsSkipped,
    int AlreadySentSkipped,
    int DeadLetterSkipped,
    IReadOnlyList<EmailNotificationRecoverabilityInspectionItem> Notifications);

public sealed record EmailNotificationRecoverabilityInspectionItem(
    Id<NotificationMessage> NotificationId,
    EmailNotificationType Type,
    string SchedulerJobId,
    EmailNotificationStatus DurableStatus,
    bool IsDeadLettered,
    EmailNotificationRecoverabilityDisposition Disposition,
    string? HangfireState,
    bool HasBrokenSchedulerState);

public enum EmailNotificationRecoverabilityDisposition
{
    Recoverable = 0,
    ActiveJobSkipped = 1,
    AlreadySentSkipped = 2,
    DeadLetterSkipped = 3,
    DurableStateSkipped = 4
}
