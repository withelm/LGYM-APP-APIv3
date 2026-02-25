using LgymApi.Application.Notifications.Models;

namespace LgymApi.Application.Notifications;

public interface IInvitationEmailScheduler
{
    Task ScheduleInvitationCreatedAsync(InvitationEmailPayload payload, CancellationToken cancellationToken = default);
}
