using LgymApi.BackgroundWorker.Common.Notifications.Models;

namespace LgymApi.BackgroundWorker.Common.Notifications;

public interface IEmailTemplateComposer
{
    string NotificationType { get; }
    EmailMessage Compose(string payloadJson);
}
