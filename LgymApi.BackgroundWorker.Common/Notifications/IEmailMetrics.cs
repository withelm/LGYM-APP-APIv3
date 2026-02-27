namespace LgymApi.BackgroundWorker.Common.Notifications;

using LgymApi.Domain.Notifications;

public interface IEmailMetrics
{
    void RecordEnqueued(EmailNotificationType notificationType);
    void RecordSent(EmailNotificationType notificationType);
    void RecordFailed(EmailNotificationType notificationType);
    void RecordRetried(EmailNotificationType notificationType);
}
