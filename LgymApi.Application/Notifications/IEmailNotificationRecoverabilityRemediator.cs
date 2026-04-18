using LgymApi.Application.Notifications.Models;

namespace LgymApi.Application.Notifications;

public interface IEmailNotificationRecoverabilityRemediator
{
    Task<EmailNotificationRecoverabilityInspectionResult> RemediateAsync(CancellationToken cancellationToken = default);
}
