using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Notifications.Domain;

namespace LgymApi.Notifications.Application.Models;

public sealed record InAppNotificationResult(
    Id<InAppNotification> Id,
    Id<User> RecipientId,
    string Message,
    string? RedirectUrl,
    bool IsRead,
    InAppNotificationType Type,
    bool IsSystemNotification,
    Id<User>? SenderUserId,
    DateTimeOffset CreatedAt);
