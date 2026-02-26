namespace LgymApi.Application.Notifications;

public interface IEmailMetrics
{
    void RecordEnqueued(string notificationType);
    void RecordSent(string notificationType);
    void RecordFailed(string notificationType);
    void RecordRetried(string notificationType);
}
