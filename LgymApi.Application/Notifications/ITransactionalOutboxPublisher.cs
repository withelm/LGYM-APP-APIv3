using LgymApi.Application.Notifications.Models;

namespace LgymApi.Application.Notifications;

public interface ITransactionalOutboxPublisher
{
    Task<Guid> PublishAsync(OutboxEventEnvelope envelope, CancellationToken cancellationToken = default);
}
