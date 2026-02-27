using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;

namespace LgymApi.Application.Repositories;

public interface IEmailNotificationLogRepository
{
    Task AddAsync(NotificationMessage message, CancellationToken cancellationToken = default);
    Task<NotificationMessage?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NotificationMessage?> FindByCorrelationAsync(EmailNotificationType type, Guid correlationId, string recipient, CancellationToken cancellationToken = default);
}
