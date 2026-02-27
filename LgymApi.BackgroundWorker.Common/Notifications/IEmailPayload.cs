using System.Globalization;

namespace LgymApi.BackgroundWorker.Common.Notifications;

public interface IEmailPayload
{
    Guid CorrelationId { get; }
    string RecipientEmail { get; }
    string NotificationType { get; }
    CultureInfo Culture { get; }
}
