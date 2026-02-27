namespace LgymApi.BackgroundWorker.Common.Notifications;

public interface IEmailScheduler<in TPayload>
    where TPayload : IEmailPayload
{
    Task ScheduleAsync(TPayload payload, CancellationToken cancellationToken = default);
}
