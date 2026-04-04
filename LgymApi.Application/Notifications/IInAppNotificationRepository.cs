using LgymApi.Application.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Notifications;

public interface IInAppNotificationRepository
{
    Task AddAsync(InAppNotification notification, CancellationToken cancellationToken = default);
    Task<InAppNotification?> GetByIdAsync(Id<InAppNotification> id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InAppNotification>> GetPageAsync(Id<User> userId, int limit, DateTimeOffset? cursorCreatedAt, Id<User>? cursorId, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Id<InAppNotification> id, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Id<User> userId, DateTimeOffset? before, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Id<User> userId, CancellationToken cancellationToken = default);
}
