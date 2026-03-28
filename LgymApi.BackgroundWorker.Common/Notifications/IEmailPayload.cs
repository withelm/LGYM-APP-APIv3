using System.Globalization;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common.Notifications;

public interface IEmailPayload
{
    Id<CorrelationScope> CorrelationId { get; }
    string RecipientEmail { get; }
    EmailNotificationType NotificationType { get; }
    CultureInfo Culture { get; }
}
