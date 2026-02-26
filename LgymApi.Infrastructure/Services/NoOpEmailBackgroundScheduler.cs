using LgymApi.Application.Notifications;

namespace LgymApi.Infrastructure.Services;

public sealed class NoOpEmailBackgroundScheduler : IEmailBackgroundScheduler
{
    public void Enqueue(Guid notificationId)
    {
    }
}
