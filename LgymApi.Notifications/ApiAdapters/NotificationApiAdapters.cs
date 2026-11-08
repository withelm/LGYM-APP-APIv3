using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;
using InAppNotificationEntity = LgymApi.Domain.Entities.InAppNotification;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Notifications.ApiAdapters;

public interface IInAppNotificationApiAdapter
{
    Task<Result<PagedResult<InAppNotificationResult>, AppError>> GetForAccountAsync(
        Id<AccountReference> accountId,
        CursorPaginationQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<Unit, AppError>> MarkAsReadAsync(
        Id<InAppNotificationEntity> notificationId,
        AuthenticatedAccountContext accountContext,
        CancellationToken cancellationToken = default);

    Task<Result<Unit, AppError>> MarkAllAsReadAsync(
        Id<AccountReference> accountId,
        DateTimeOffset? before,
        CancellationToken cancellationToken = default);

    Task<Result<int, AppError>> GetUnreadCountAsync(
        Id<AccountReference> accountId,
        CancellationToken cancellationToken = default);
}

public sealed record EnqueueAccountNotificationEventInput(
    Id<AccountReference> RecipientAccountId,
    int SchemaVersion,
    string Type,
    string EventKey,
    string? EntityKey,
    Id<InAppNotificationEntity>? InAppNotificationId,
    string? Deeplink);

public interface INotificationEventApiAdapter
{
    Task EnqueueAsync(
        EnqueueAccountNotificationEventInput input,
        CancellationToken cancellationToken = default);
}

internal sealed class InAppNotificationApiAdapter : IInAppNotificationApiAdapter
{
    private readonly IInAppNotificationService _notificationService;
    private readonly IAuthenticatedAccountAuthorizationService _authorizationService;

    public InAppNotificationApiAdapter(
        IInAppNotificationService notificationService,
        IAuthenticatedAccountAuthorizationService authorizationService)
    {
        _notificationService = notificationService;
        _authorizationService = authorizationService;
    }

    public Task<Result<PagedResult<InAppNotificationResult>, AppError>> GetForAccountAsync(
        Id<AccountReference> accountId,
        CursorPaginationQuery query,
        CancellationToken cancellationToken = default)
        => _notificationService.GetForUserAsync(accountId.Rebind<UserEntity>(), query, cancellationToken);

    public Task<Result<Unit, AppError>> MarkAsReadAsync(
        Id<InAppNotificationEntity> notificationId,
        AuthenticatedAccountContext accountContext,
        CancellationToken cancellationToken = default)
        => _notificationService.MarkAsReadAsync(
            notificationId,
            _authorizationService.CreateActor(accountContext),
            cancellationToken);

    public Task<Result<Unit, AppError>> MarkAllAsReadAsync(
        Id<AccountReference> accountId,
        DateTimeOffset? before,
        CancellationToken cancellationToken = default)
        => _notificationService.MarkAllAsReadAsync(accountId.Rebind<UserEntity>(), before, cancellationToken);

    public Task<Result<int, AppError>> GetUnreadCountAsync(
        Id<AccountReference> accountId,
        CancellationToken cancellationToken = default)
        => _notificationService.GetUnreadCountAsync(accountId.Rebind<UserEntity>(), cancellationToken);
}

internal sealed class NotificationEventApiAdapter : INotificationEventApiAdapter
{
    private readonly INotificationEventBridge _notificationEventBridge;

    public NotificationEventApiAdapter(INotificationEventBridge notificationEventBridge)
    {
        _notificationEventBridge = notificationEventBridge;
    }

    public Task EnqueueAsync(
        EnqueueAccountNotificationEventInput input,
        CancellationToken cancellationToken = default)
        => _notificationEventBridge.EnqueueAsync(
            new EnqueueNotificationEventInput(
                input.RecipientAccountId.Rebind<UserEntity>(),
                input.SchemaVersion,
                input.Type,
                input.EventKey,
                input.EntityKey,
                input.InAppNotificationId,
                input.Deeplink),
            cancellationToken);
}
