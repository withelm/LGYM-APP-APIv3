using LgymApi.BackgroundWorker.Common.Notifications.Models;

namespace LgymApi.BackgroundWorker.Common.Notifications;

public interface IEmailTemplateComposerFactory
{
    EmailMessage ComposeMessage(string notificationType, string payloadJson);
}
