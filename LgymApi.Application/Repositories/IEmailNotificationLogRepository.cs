using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IEmailNotificationLogRepository
{
    Task AddAsync(NotificationMessage message, CancellationToken cancellationToken = default);
    Task<NotificationMessage?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NotificationMessage?> FindByCorrelationAsync(string type, Guid correlationId, string recipient, CancellationToken cancellationToken = default);
}
