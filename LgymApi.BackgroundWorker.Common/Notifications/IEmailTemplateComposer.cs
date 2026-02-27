using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Notifications;

namespace LgymApi.BackgroundWorker.Common.Notifications;

public interface IEmailTemplateComposer
{
    EmailNotificationType NotificationType { get; }
    EmailMessage Compose(string payloadJson);
}
