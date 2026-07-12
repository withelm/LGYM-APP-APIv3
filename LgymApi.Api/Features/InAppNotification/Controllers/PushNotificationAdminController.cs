using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.InAppNotification.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InAppNotificationEntity = global::LgymApi.Domain.Entities.InAppNotification;
using UserEntity = global::LgymApi.Domain.Entities.User;

namespace LgymApi.Api.Features.InAppNotification.Controllers;

[ApiController]
[Route("api/internal/push")]
[Authorize(Policy = AuthConstants.Policies.AdminAccess)]
public sealed class PushNotificationAdminController : ControllerBase
{
    private const int SchemaVersion = 1;

    private readonly INotificationEventBridge _notificationEventBridge;
    private readonly IMapper _mapper;

    public PushNotificationAdminController(INotificationEventBridge notificationEventBridge, IMapper mapper)
    {
        _notificationEventBridge = notificationEventBridge;
        _mapper = mapper;
    }

    [HttpPost("test-event")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> EnqueueTestEvent(
        [FromBody] EnqueueTestPushEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = new EnqueueNotificationEventInput(
            request.RecipientUserId.ToIdOrEmpty<UserEntity>(),
            SchemaVersion,
            request.Type,
            request.EventId,
            request.EntityId,
            request.InAppNotificationId.ToNullableId<InAppNotificationEntity>(),
            request.Deeplink);

        await _notificationEventBridge.EnqueueAsync(input, cancellationToken);

        return Ok(_mapper.Map<string, ResponseMessageDto>("Push test event queued"));
    }
}
