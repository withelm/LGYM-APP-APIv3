using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.InAppNotification.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Mapping.Core;
using LgymApi.Notifications.Application;
using LgymApi.Notifications.Application.Models;
using LgymApi.Resources;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.InAppNotification.Controllers;

[ApiController]
[Route("api")]
public sealed class InAppNotificationController : ControllerBase
{
    private readonly IInAppNotificationService _notificationService;
    private readonly IMapper _mapper;

    public InAppNotificationController(IInAppNotificationService notificationService, IMapper mapper)
    {
        _notificationService = notificationService;
        _mapper = mapper;
    }

    [HttpGet("{id}/notifications")]
    [ProducesResponseType(typeof(PagedNotificationsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetNotifications(
        [FromRoute] string id,
        [FromQuery] GetNotificationsQueryDto query)
    {
        var userId = HttpContext.ParseRouteUserIdForCurrentUser(id);
        var cursorQuery = new CursorPaginationQuery(query.Limit, query.CursorCreatedAt, query.CursorId);
        var result = await _notificationService.GetForUserAsync(userId, cursorQuery, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var mapped = _mapper.Map<PagedResult<InAppNotificationResult>, PagedNotificationsResultDto>(result.Value);
        return Ok(mapped);
    }

    [HttpPost("{id}/notifications/{notificationId}/mark-read")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(
        [FromRoute] string id,
        [FromRoute] string notificationId)
    {
        var userId = HttpContext.ParseRouteUserIdForCurrentUser(id);
        var notifId = notificationId.ToIdOrEmpty<Notifications.Domain.InAppNotification>();
        var result = await _notificationService.MarkAsReadAsync(notifId, userId, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.InAppNotificationMarkedAsRead));
    }

    [HttpPost("{id}/notifications/mark-all-read")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkAllAsRead(
        [FromRoute] string id,
        [FromQuery] DateTimeOffset? before)
    {
        var userId = HttpContext.ParseRouteUserIdForCurrentUser(id);
        var result = await _notificationService.MarkAllAsReadAsync(userId, before, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.InAppNotificationAllMarkedAsRead));
    }

    [HttpGet("{id}/notifications/unread-count")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUnreadCount([FromRoute] string id)
    {
        var userId = HttpContext.ParseRouteUserIdForCurrentUser(id);
        var result = await _notificationService.GetUnreadCountAsync(userId, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(new UnreadCountDto(result.Value));
    }
}
