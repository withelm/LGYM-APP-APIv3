using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common.Push.Models;

public sealed record PushEventPayload(
    int SchemaVersion,
    string Type,
    string EventId,
    string? EntityId,
    Id<InAppNotification>? InAppNotificationId,
    string? Deeplink);
