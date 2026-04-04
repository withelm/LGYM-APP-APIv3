using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Notifications.Domain;

namespace LgymApi.Notifications.Application.Models;

public sealed record CreateInAppNotificationInput(
    Id<User> RecipientId,
    Id<User>? SenderUserId,
    bool IsSystemNotification,
    string Message,
    string? RedirectUrl,
    InAppNotificationType Type);
