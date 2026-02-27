using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Notifications;

namespace LgymApi.BackgroundWorker.Common.Notifications;

public interface IEmailTemplateComposerFactory
{
    EmailMessage ComposeMessage(EmailNotificationType notificationType, string payloadJson);
}
