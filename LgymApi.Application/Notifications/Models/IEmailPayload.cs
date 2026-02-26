using System.Globalization;

namespace LgymApi.Application.Notifications.Models;

public interface IEmailPayload
{
    Guid CorrelationId { get; }
    string RecipientEmail { get; }
    string NotificationType { get; }
    CultureInfo Culture { get; }
}
