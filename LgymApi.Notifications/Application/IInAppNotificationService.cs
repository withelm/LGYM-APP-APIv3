using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Notifications.Application.Models;
using LgymApi.Notifications.Domain;

namespace LgymApi.Notifications.Application;

public interface IInAppNotificationService
{
    Task<Result<InAppNotificationResult, AppError>> CreateAsync(CreateInAppNotificationInput input, CancellationToken ct = default);
    Task<Result<PagedResult<InAppNotificationResult>, AppError>> GetForUserAsync(Id<User> userId, CursorPaginationQuery query, CancellationToken ct = default);
    Task<Result<Unit, AppError>> MarkAsReadAsync(Id<InAppNotification> notificationId, Id<User> requestingUserId, CancellationToken ct = default);
    Task<Result<Unit, AppError>> MarkAllAsReadAsync(Id<User> userId, DateTimeOffset? before, CancellationToken ct = default);
    Task<Result<int, AppError>> GetUnreadCountAsync(Id<User> userId, CancellationToken ct = default);
}
