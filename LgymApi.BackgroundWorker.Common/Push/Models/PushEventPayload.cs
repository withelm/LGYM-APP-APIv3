namespace LgymApi.BackgroundWorker.Common.Push.Models;

public sealed record PushEventPayload(
    int SchemaVersion,
    string Type,
    string EventId,
    string? EntityId,
    string? InAppNotificationId,
    string? Deeplink);
