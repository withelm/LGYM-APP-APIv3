using LgymApi.Application.Notifications;

namespace LgymApi.Infrastructure.Services;

public sealed class NoOpInvitationEmailBackgroundScheduler : IInvitationEmailBackgroundScheduler
{
    public void Enqueue(Guid notificationId)
    {
    }
}
