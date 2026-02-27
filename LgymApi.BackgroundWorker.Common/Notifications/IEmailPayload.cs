using System.Globalization;
using LgymApi.Domain.Notifications;

namespace LgymApi.BackgroundWorker.Common.Notifications;

public interface IEmailPayload
{
    Guid CorrelationId { get; }
    string RecipientEmail { get; }
    EmailNotificationType NotificationType { get; }
    CultureInfo Culture { get; }
}
