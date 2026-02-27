namespace LgymApi.Domain.Notifications;

public static class EmailNotificationTypes
{
    public static readonly EmailNotificationType Welcome = EmailNotificationType.Define("user.registration.welcome");
    public static readonly EmailNotificationType TrainerInvitation = EmailNotificationType.Define("trainer.invitation.created");
    public static readonly EmailNotificationType TrainingCompleted = EmailNotificationType.Define("training.completed");

    public static IReadOnlyCollection<EmailNotificationType> All { get; } =
    [
        Welcome,
        TrainerInvitation,
        TrainingCompleted
    ];

    public static bool TryFromValue(string? value, out EmailNotificationType notificationType)
    {
        notificationType = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var candidate in All)
        {
            if (string.Equals(candidate.Value, value, StringComparison.Ordinal))
            {
                notificationType = candidate;
                return true;
            }
        }

        return false;
    }
}
