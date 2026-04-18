using LgymApi.Application.Notifications.Models;

namespace LgymApi.Application.Notifications;

public interface IEmailNotificationRecoverabilityInspector
{
    Task<EmailNotificationRecoverabilityInspectionResult> InspectAsync(CancellationToken cancellationToken = default);
}
