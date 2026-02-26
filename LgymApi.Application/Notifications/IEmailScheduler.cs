using LgymApi.Application.Notifications.Models;

namespace LgymApi.Application.Notifications;

public interface IEmailScheduler<in TPayload>
    where TPayload : IEmailPayload
{
    Task ScheduleAsync(TPayload payload, CancellationToken cancellationToken = default);
}
