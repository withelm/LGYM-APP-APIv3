namespace LgymApi.Domain.Notifications;

public static class OutboxEventTypes
{
    public static readonly OutboxEventType EmailNotificationScheduled = OutboxEventType.Define("email.notification.scheduled");

    public static IReadOnlyCollection<OutboxEventType> All { get; } =
    [
        EmailNotificationScheduled
    ];

    public static bool TryFromValue(string? value, out OutboxEventType outboxEventType)
    {
        outboxEventType = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var candidate in All)
        {
            if (string.Equals(candidate.Value, value, StringComparison.Ordinal))
            {
                outboxEventType = candidate;
                return true;
            }
        }

        return false;
    }
}
