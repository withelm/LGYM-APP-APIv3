namespace LgymApi.Application.Notifications;

public interface IWelcomeEmailMetrics
{
    void RecordEnqueued();
    void RecordSent();
    void RecordFailed();
    void RecordRetried();
}
