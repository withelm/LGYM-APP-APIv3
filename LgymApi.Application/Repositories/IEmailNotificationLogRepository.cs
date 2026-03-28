using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface IEmailNotificationLogRepository
{
    Task AddAsync(NotificationMessage message, CancellationToken cancellationToken = default);
    Task<NotificationMessage?> FindByIdAsync(Id<NotificationMessage> id, CancellationToken cancellationToken = default);
    Task<NotificationMessage?> FindByCorrelationAsync(EmailNotificationType type, Id<CorrelationScope> correlationId, string recipient, CancellationToken cancellationToken = default);
}
