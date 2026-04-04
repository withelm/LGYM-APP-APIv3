using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Notifications.Application.Models;
using LgymApi.Notifications.Application.Errors;
using LgymApi.Notifications.Domain;

namespace LgymApi.Notifications.Application;

public sealed class InAppNotificationService : IInAppNotificationService
{
    private readonly IInAppNotificationServiceDependencies _deps;

    public InAppNotificationService(IInAppNotificationServiceDependencies deps)
    {
        _deps = deps;
    }

    public async Task<Result<InAppNotificationResult, AppError>> CreateAsync(
        CreateInAppNotificationInput input,
        CancellationToken ct = default)
    {
        var notification = new InAppNotification
        {
            Id = Id<InAppNotification>.New(),
            RecipientId = input.RecipientId,
            SenderUserId = input.SenderUserId,
            IsSystemNotification = input.IsSystemNotification,
            Message = input.Message,
            RedirectUrl = input.RedirectUrl,
            IsRead = false,
            Type = input.Type,
        };

        await _deps.InAppNotificationRepository.AddAsync(notification, ct);
        await _deps.UnitOfWork.SaveChangesAsync(ct);

        var result = MapToResult(notification);

        try
        {
            await _deps.PushPublisher.PushAsync(result, ct);
        }
        catch
        {
        }

        return Result<InAppNotificationResult, AppError>.Success(result);
    }

    public async Task<Result<PagedResult<InAppNotificationResult>, AppError>> GetForUserAsync(
        Id<User> userId,
        CursorPaginationQuery query,
        CancellationToken ct = default)
    {
        var items = await _deps.InAppNotificationRepository.GetPageAsync(
            userId, query.Limit + 1, query.CursorCreatedAt, query.CursorId, ct);

        var hasNextPage = items.Count > query.Limit;
        if (hasNextPage)
        {
            items = items.Take(query.Limit).ToList();
        }

        var lastItem = items.LastOrDefault();
        var resultItems = items.Select(MapToResult).ToList();

        return Result<PagedResult<InAppNotificationResult>, AppError>.Success(new PagedResult<InAppNotificationResult>(
            resultItems,
            hasNextPage,
            hasNextPage ? lastItem?.CreatedAt : null,
            hasNextPage ? (Guid?)lastItem?.Id.GetValue() : null));
    }

    public async Task<Result<Unit, AppError>> MarkAsReadAsync(
        Id<InAppNotification> notificationId,
        Id<User> requestingUserId,
        CancellationToken ct = default)
    {
        var notification = await _deps.InAppNotificationRepository.GetByIdAsync(notificationId, ct);
        if (notification == null)
        {
            return Result<Unit, AppError>.Failure(new InAppNotificationNotFoundError());
        }

        if (notification.RecipientId != requestingUserId)
        {
            return Result<Unit, AppError>.Failure(new InAppNotificationForbiddenError());
        }

        await _deps.InAppNotificationRepository.MarkAsReadAsync(notificationId, ct);
        await _deps.UnitOfWork.SaveChangesAsync(ct);

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> MarkAllAsReadAsync(
        Id<User> userId,
        DateTimeOffset? before,
        CancellationToken ct = default)
    {
        await _deps.InAppNotificationRepository.MarkAllAsReadAsync(userId, before, ct);
        await _deps.UnitOfWork.SaveChangesAsync(ct);

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<int, AppError>> GetUnreadCountAsync(
        Id<User> userId,
        CancellationToken ct = default)
    {
        var count = await _deps.InAppNotificationRepository.GetUnreadCountAsync(userId, ct);
        return Result<int, AppError>.Success(count);
    }

    private static InAppNotificationResult MapToResult(InAppNotification notification)
    {
        return new InAppNotificationResult(
            notification.Id,
            notification.RecipientId,
            notification.Message,
            notification.RedirectUrl,
            notification.IsRead,
            notification.Type,
            notification.IsSystemNotification,
            notification.SenderUserId,
            notification.CreatedAt);
    }
}
