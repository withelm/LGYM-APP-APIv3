using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface IEmailNotificationSubscriptionRepository
{
    Task<bool> IsSubscribedAsync(Id<User> userId, string notificationType, CancellationToken cancellationToken = default);
}
