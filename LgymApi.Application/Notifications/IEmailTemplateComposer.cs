using LgymApi.Application.Notifications.Models;

namespace LgymApi.Application.Notifications;

public interface IEmailTemplateComposer
{
    string NotificationType { get; }
    EmailMessage Compose(string payloadJson);
}
