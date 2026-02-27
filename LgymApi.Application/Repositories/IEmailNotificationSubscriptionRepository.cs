namespace LgymApi.Application.Repositories;

public interface IEmailNotificationSubscriptionRepository
{
    Task<bool> IsSubscribedAsync(Guid userId, string notificationType, CancellationToken cancellationToken = default);
}
