using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Notifications.Errors;
using LgymApi.Application.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Notifications;

public sealed class InAppNotificationService : IInAppNotificationService
{
    private readonly IInAppNotificationServiceDependencies _deps;
    private readonly ILogger<InAppNotificationService> _logger;

    public InAppNotificationService(IInAppNotificationServiceDependencies deps, ILogger<InAppNotificationService> logger)
    {
        _deps = deps;
        _logger = logger;
    }

    public async Task<Result<InAppNotificationResult, AppError>> CreateAsync(CreateInAppNotificationInput input, CancellationToken cancellationToken = default)
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

        await _deps.InAppNotificationRepository.AddAsync(notification, cancellationToken);
        await _deps.UnitOfWork.SaveChangesAsync(cancellationToken);

        var result = MapToResult(notification);

        try
        {
            await _deps.PushPublisher.PushAsync(result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push notification for recipient {RecipientId}", input.RecipientId);
        }

        return Result<InAppNotificationResult, AppError>.Success(result);
    }

    public async Task<Result<PagedResult<InAppNotificationResult>, AppError>> GetForUserAsync(Id<User> userId, CursorPaginationQuery query, CancellationToken cancellationToken = default)
    {
        var items = await _deps.InAppNotificationRepository.GetPageAsync(userId, query.Limit + 1, query.CursorCreatedAt, query.CursorId, cancellationToken);

        var hasNextPage = items.Count > query.Limit;
        if (hasNextPage)
        {
            items = items.Take(query.Limit).ToList();
        }

        var lastItem = items.LastOrDefault();
        var resultItems = items.Select(MapToResult).ToList();

        return Result<PagedResult<InAppNotificationResult>, AppError>.Success(new PagedResult<InAppNotificationResult>(resultItems, hasNextPage, hasNextPage ? lastItem?.CreatedAt : null, hasNextPage ? lastItem?.Id.Rebind<User>() : null));
    }

    public async Task<Result<Unit, AppError>> MarkAsReadAsync(Id<InAppNotification> notificationId, Id<User> requestingUserId, CancellationToken cancellationToken = default)
    {
        var notification = await _deps.InAppNotificationRepository.GetByIdAsync(notificationId, cancellationToken);
        if (notification == null)
        {
            return Result<Unit, AppError>.Failure(new InAppNotificationNotFoundError());
        }

        if (notification.RecipientId != requestingUserId)
        {
            return Result<Unit, AppError>.Failure(new InAppNotificationForbiddenError());
        }

        await _deps.InAppNotificationRepository.MarkAsReadAsync(notificationId, cancellationToken);
        await _deps.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> MarkAllAsReadAsync(Id<User> userId, DateTimeOffset? before, CancellationToken cancellationToken = default)
    {
        await _deps.InAppNotificationRepository.MarkAllAsReadAsync(userId, before, cancellationToken);
        await _deps.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<int, AppError>> GetUnreadCountAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        var count = await _deps.InAppNotificationRepository.GetUnreadCountAsync(userId, cancellationToken);
        return Result<int, AppError>.Success(count);
    }

    private static InAppNotificationResult MapToResult(InAppNotification notification)
        => new(notification.Id, notification.RecipientId, notification.Message, notification.RedirectUrl, notification.IsRead, notification.Type, notification.IsSystemNotification, notification.SenderUserId, notification.CreatedAt);
}
