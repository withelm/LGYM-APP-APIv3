using LgymApi.Application.Notifications.Models;

namespace LgymApi.Application.Notifications;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
