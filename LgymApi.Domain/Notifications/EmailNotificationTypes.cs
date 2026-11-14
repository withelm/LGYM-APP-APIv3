namespace LgymApi.Domain.Notifications;

public static class EmailNotificationTypes
{
    public static readonly EmailNotificationType Welcome = EmailNotificationType.Define("user.registration.welcome");
    public static readonly EmailNotificationType TrainerInvitation = EmailNotificationType.Define("trainer.invitation.created");
    public static readonly EmailNotificationType TrainerInvitationAccepted = EmailNotificationType.Define("trainer.invitation.accepted");
    public static readonly EmailNotificationType TrainerInvitationRevoked = EmailNotificationType.Define("trainer.invitation.revoked");
    public static readonly EmailNotificationType TrainingCompleted = EmailNotificationType.Define("training.completed");
    public static readonly EmailNotificationType PasswordRecovery = EmailNotificationType.Define("user.password.recovery");
    public static readonly EmailNotificationType AccountRecovery = EmailNotificationType.Define("user.account.recovery");

    public static IReadOnlyCollection<EmailNotificationType> All { get; } =
    [
        Welcome,
        TrainerInvitation,
        TrainerInvitationAccepted,
        TrainerInvitationRevoked,
        TrainingCompleted,
        PasswordRecovery,
        AccountRecovery
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
