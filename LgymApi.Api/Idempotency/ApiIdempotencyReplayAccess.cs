using System.Text.Json;
using LgymApi.Application.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Entities;
using LgymApi.Api.Middleware;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;

namespace LgymApi.Api.Idempotency;

public sealed record ApiIdempotencyReplayAccessContext(
    HttpContext HttpContext,
    ApiIdempotencyAttribute Metadata,
    AuthenticatedAccountContext Account);

public interface IApiIdempotencyReplayAccessPort
{
    ApiIdempotencyReplayOwner Owner { get; }

    Task<bool> CanReplayAsync(
        ApiIdempotencyReplayAccessContext context,
        CancellationToken cancellationToken = default);
}

public sealed class ApiIdempotencyReplayAccessEvaluator
{
    private readonly IReadOnlyDictionary<ApiIdempotencyReplayOwner, IApiIdempotencyReplayAccessPort> _ports;

    public ApiIdempotencyReplayAccessEvaluator(IEnumerable<IApiIdempotencyReplayAccessPort> ports)
    {
        _ports = ports.ToDictionary(port => port.Owner);
    }

    public Task<bool> CanReplayAsync(HttpContext context, ApiIdempotencyAttribute metadata, CancellationToken cancellationToken)
    {
        if (metadata.ReplayOwner == ApiIdempotencyReplayOwner.Public)
        {
            return Task.FromResult(true);
        }

        if (metadata.ReplayOwner == ApiIdempotencyReplayOwner.NoReplay
            || context.Features.Get<IAuthenticatedAccountContextFeature>() is not { } accountFeature
            || !_ports.TryGetValue(metadata.ReplayOwner, out var port))
        {
            return Task.FromResult(false);
        }

        return port.CanReplayAsync(
            new ApiIdempotencyReplayAccessContext(context, metadata, accountFeature.Context),
            cancellationToken);
    }
}

internal sealed class IdentityIdempotencyReplayAccessPort : IApiIdempotencyReplayAccessPort
{
    private readonly IAuthenticatedAccountContextResolver _contextResolver;
    private readonly IAuthenticatedAccountAuthorizationService _authorizationService;

    public IdentityIdempotencyReplayAccessPort(
        IAuthenticatedAccountContextResolver contextResolver,
        IAuthenticatedAccountAuthorizationService authorizationService)
    {
        _contextResolver = contextResolver;
        _authorizationService = authorizationService;
    }

    public ApiIdempotencyReplayOwner Owner => ApiIdempotencyReplayOwner.Identity;

    public async Task<bool> CanReplayAsync(
        ApiIdempotencyReplayAccessContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Account.SessionId is not { } sessionId)
        {
            return false;
        }

        var resolution = await _contextResolver.ResolveAsync(context.Account.Id, sessionId, cancellationToken);
        if (resolution.Status != AuthenticatedAccountResolutionStatus.Active || resolution.Context is not { } currentAccount)
        {
            return false;
        }

        if (context.Metadata.ReplayResourceBinding == ApiIdempotencyReplayResourceBinding.RouteAccount
            && (!context.HttpContext.Request.RouteValues.TryGetValue("id", out var routeValue)
                || !Id<AccountReference>.TryParse(routeValue?.ToString() ?? string.Empty, out var routeAccountId)
                || routeAccountId != currentAccount.Id))
        {
            return false;
        }

        return context.Metadata.RequiredPermission is null
            || _authorizationService.HasPermission(currentAccount, context.Metadata.RequiredPermission);
    }
}

internal sealed class NotificationsIdempotencyReplayAccessPort : IApiIdempotencyReplayAccessPort
{
    private readonly IAuthenticatedAccountContextResolver _contextResolver;
    private readonly INotificationReplayAccessService _notificationReplayAccessService;

    public NotificationsIdempotencyReplayAccessPort(
        IAuthenticatedAccountContextResolver contextResolver,
        INotificationReplayAccessService notificationReplayAccessService)
    {
        _contextResolver = contextResolver;
        _notificationReplayAccessService = notificationReplayAccessService;
    }

    public ApiIdempotencyReplayOwner Owner => ApiIdempotencyReplayOwner.Notifications;

    public async Task<bool> CanReplayAsync(
        ApiIdempotencyReplayAccessContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Account.SessionId is not { } sessionId)
        {
            return false;
        }

        var resolution = await _contextResolver.ResolveAsync(context.Account.Id, sessionId, cancellationToken);
        if (resolution.Status != AuthenticatedAccountResolutionStatus.Active
            || resolution.Context is not { } currentAccount)
        {
            return false;
        }

        if (context.Metadata.ReplayResourceBinding == ApiIdempotencyReplayResourceBinding.RouteAccount
            && (!context.HttpContext.Request.RouteValues.TryGetValue("id", out var routeValue)
                || !Id<AccountReference>.TryParse(routeValue?.ToString() ?? string.Empty, out var routeAccountId)
                || routeAccountId != currentAccount.Id))
        {
            return false;
        }

        if (currentAccount.Id != context.Account.Id)
        {
            return false;
        }

        return context.Metadata.RouteTemplate switch
        {
            "api/{id}/notifications/{notificationId}/mark-read" =>
                await CanReplayMarkAsReadAsync(context.HttpContext, currentAccount, cancellationToken),
            "api/{id}/notifications/mark-all-read" => true,
            "api/push/installations/register" =>
                await CanReplayInstallationAsync(context.HttpContext, currentAccount, NotificationReplayResourceKind.RegisterInstallation, cancellationToken),
            "api/push/installations/unregister" =>
                await CanReplayInstallationAsync(context.HttpContext, currentAccount, NotificationReplayResourceKind.UnregisterInstallation, cancellationToken),
            _ => false
        };
    }

    private Task<bool> CanReplayMarkAsReadAsync(
        HttpContext context,
        AuthenticatedAccountContext account,
        CancellationToken cancellationToken)
    {
        return context.Request.RouteValues.TryGetValue("notificationId", out var routeValue)
            && Id<InAppNotification>.TryParse(routeValue?.ToString() ?? string.Empty, out var notificationId)
                ? _notificationReplayAccessService.CanReplayNotificationAsync(account, notificationId, cancellationToken)
                : Task.FromResult(false);
    }

    private async Task<bool> CanReplayInstallationAsync(
        HttpContext context,
        AuthenticatedAccountContext account,
        NotificationReplayResourceKind resourceKind,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Body.CanSeek)
        {
            return false;
        }

        string? installationId;
        try
        {
            context.Request.Body.Position = 0;
            using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("installationId", out var installationIdElement)
                || installationIdElement.ValueKind != JsonValueKind.String
                || installationIdElement.GetString() is not { } parsedInstallationId
                || string.IsNullOrWhiteSpace(parsedInstallationId))
            {
                return false;
            }

            installationId = parsedInstallationId.Trim();
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        finally
        {
            context.Request.Body.Position = 0;
        }

        return await _notificationReplayAccessService.CanReplayInstallationAsync(
            account,
            installationId,
            resourceKind,
            cancellationToken);
    }
}
