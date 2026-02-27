using LgymApi.BackgroundWorker.Common.Notifications.Models;

namespace LgymApi.BackgroundWorker.Common.Notifications;

public interface IEmailSender
{
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
