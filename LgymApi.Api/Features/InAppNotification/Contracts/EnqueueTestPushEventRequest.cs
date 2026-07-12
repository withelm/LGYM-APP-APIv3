using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.InAppNotification.Contracts;

public sealed record EnqueueTestPushEventRequest(
    [property: JsonPropertyName("recipientUserId")] string RecipientUserId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("entityId")] string? EntityId,
    [property: JsonPropertyName("inAppNotificationId")] string? InAppNotificationId,
    [property: JsonPropertyName("deeplink")] string? Deeplink) : IDto;
