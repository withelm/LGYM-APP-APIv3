using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.InAppNotification.Contracts;

public sealed record InAppNotificationResultDto(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("redirectUrl")] string? RedirectUrl,
    [property: JsonPropertyName("isRead")] bool IsRead,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("isSystemNotification")] bool IsSystemNotification,
    [property: JsonPropertyName("senderUserId")] string? SenderUserId,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt) : IResultDto;

public sealed record GetNotificationsQueryDto(
    [property: JsonPropertyName("limit")] int Limit = 20,
    [property: JsonPropertyName("cursorCreatedAt")] DateTimeOffset? CursorCreatedAt = null,
    [property: JsonPropertyName("cursorId")] string? CursorId = null) : IDto;

public sealed record PagedNotificationsResultDto(
    [property: JsonPropertyName("items")] IReadOnlyList<InAppNotificationResultDto> Items,
    [property: JsonPropertyName("hasNextPage")] bool HasNextPage,
    [property: JsonPropertyName("nextCursorCreatedAt")] DateTimeOffset? NextCursorCreatedAt,
    [property: JsonPropertyName("nextCursorId")] string? NextCursorId) : IResultDto;

public sealed record UnreadCountDto(
    [property: JsonPropertyName("count")] int Count) : IResultDto;
