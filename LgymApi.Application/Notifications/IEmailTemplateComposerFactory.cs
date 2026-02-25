using LgymApi.Application.Notifications.Models;

namespace LgymApi.Application.Notifications;

public interface IEmailTemplateComposerFactory
{
    EmailMessage ComposeMessage(string notificationType, string payloadJson);
}
