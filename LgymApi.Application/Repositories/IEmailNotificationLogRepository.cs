using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IEmailNotificationLogRepository
{
    Task AddAsync(EmailNotificationLog log, CancellationToken cancellationToken = default);
    Task<EmailNotificationLog?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmailNotificationLog?> FindByCorrelationAsync(string type, Guid correlationId, string recipientEmail, CancellationToken cancellationToken = default);
}
