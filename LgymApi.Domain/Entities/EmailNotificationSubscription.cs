namespace LgymApi.Domain.Entities;

public sealed class EmailNotificationSubscription : EntityBase
{
    public Guid UserId { get; set; }
    public string NotificationType { get; set; } = string.Empty;

    public User User { get; set; } = null!;
}
