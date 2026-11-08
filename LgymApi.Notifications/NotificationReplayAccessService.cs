using LgymApi.Application.Notifications.Authorization;
using LgymApi.Application.Notifications.Repositories;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;
using LgymApi.Platform.Contracts;

namespace LgymApi.Application.Notifications;

public enum NotificationReplayResourceKind
{
    MarkAsRead,
    RegisterInstallation,
    UnregisterInstallation
}

public interface INotificationReplayAccessService
{
    Task<bool> CanReplayNotificationAsync(
        AuthenticatedAccountContext accountContext,
        Id<InAppNotification> notificationId,
        CancellationToken cancellationToken = default);

    Task<bool> CanReplayInstallationAsync(
        AuthenticatedAccountContext accountContext,
        string installationId,
        NotificationReplayResourceKind resourceKind,
        CancellationToken cancellationToken = default);
}

internal sealed class NotificationReplayAccessService(
    IInAppNotificationRepository notificationRepository,
    IPushInstallationRepository installationRepository,
    IAuthenticatedAccountAuthorizationService accountAuthorizationService,
    IAuthorizationEvaluator<NotificationOwnershipContext> ownershipEvaluator,
    NotificationDisclosureEligibilityService disclosureEligibility) : INotificationReplayAccessService
{
    public async Task<bool> CanReplayNotificationAsync(
        AuthenticatedAccountContext accountContext,
        Id<InAppNotification> notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await notificationRepository.GetByIdAsync(notificationId, cancellationToken);
        if (notification == null)
        {
            return false;
        }

        var actor = accountAuthorizationService.CreateActor(accountContext);
        return ownershipEvaluator.Evaluate(
                actor,
                new NotificationOwnershipContext(notification.RecipientId.Rebind<ActorReference>())).IsAllowed
            && await disclosureEligibility.CanDiscloseAsync(notification, cancellationToken);
    }

    public async Task<bool> CanReplayInstallationAsync(
        AuthenticatedAccountContext accountContext,
        string installationId,
        NotificationReplayResourceKind resourceKind,
        CancellationToken cancellationToken = default)
    {
        if (resourceKind is not (NotificationReplayResourceKind.RegisterInstallation
            or NotificationReplayResourceKind.UnregisterInstallation)
            || accountContext.SessionId is not { } sessionId)
        {
            return false;
        }

        var installation = await installationRepository.FindBoundToUserAndSessionAsync(
            installationId,
            accountContext.Id.Rebind<User>(),
            sessionId.Rebind<UserSession>(),
            cancellationToken);
        return installation != null;
    }
}
