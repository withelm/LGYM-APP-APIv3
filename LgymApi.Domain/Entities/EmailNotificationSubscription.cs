using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class EmailNotificationSubscription : EntityBase<EmailNotificationSubscription>
{
    public Id<User> UserId { get; set; }
    public string NotificationType { get; set; } = string.Empty;

    public User User { get; set; } = null!;
}
