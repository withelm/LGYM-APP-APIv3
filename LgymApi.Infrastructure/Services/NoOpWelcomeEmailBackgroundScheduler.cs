using LgymApi.Application.Notifications;

namespace LgymApi.Infrastructure.Services;

public sealed class NoOpWelcomeEmailBackgroundScheduler : IWelcomeEmailBackgroundScheduler
{
    public void Enqueue(Guid notificationId)
    {
    }
}
