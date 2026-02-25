using LgymApi.Application.Notifications.Models;

namespace LgymApi.Application.Notifications;

public interface IWelcomeEmailScheduler
{
    Task ScheduleWelcomeAsync(WelcomeEmailPayload payload, CancellationToken cancellationToken = default);
}
