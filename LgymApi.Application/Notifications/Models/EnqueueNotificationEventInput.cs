using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Notifications.Models;

public sealed record EnqueueNotificationEventInput(
    Id<User> UserId,
    int SchemaVersion,
    string Type,
    string EventId,
    string? EntityId,
    Id<InAppNotification>? InAppNotificationId,
    string? Deeplink);
