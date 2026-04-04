using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Notifications.Domain;

namespace LgymApi.Notifications.Application;

public interface IInAppNotificationRepository
{
    Task AddAsync(InAppNotification notification, CancellationToken cancellationToken = default);
    Task<InAppNotification?> GetByIdAsync(Id<InAppNotification> id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InAppNotification>> GetPageAsync(
        Id<User> userId,
        int limit,
        DateTimeOffset? cursorCreatedAt,
        Guid? cursorId,
        CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Id<InAppNotification> id, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Id<User> userId, DateTimeOffset? before, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Id<User> userId, CancellationToken cancellationToken = default);
}
