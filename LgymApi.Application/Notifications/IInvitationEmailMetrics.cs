namespace LgymApi.Application.Notifications;

public interface IInvitationEmailMetrics
{
    void RecordEnqueued();
    void RecordSent();
    void RecordFailed();
    void RecordRetried();
}
