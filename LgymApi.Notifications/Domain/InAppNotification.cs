using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Notifications.Domain;

public sealed class InAppNotification : EntityBase<InAppNotification>
{
    public Id<User> RecipientId { get; set; }
    public Id<User>? SenderUserId { get; set; }
    public bool IsSystemNotification { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? RedirectUrl { get; set; }
    public bool IsRead { get; set; }
    public InAppNotificationType Type { get; set; } = InAppNotificationTypes.InvitationSent;
}
