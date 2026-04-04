namespace LgymApi.Domain.Notifications;

public static class InAppNotificationTypes
{
    public static readonly InAppNotificationType InvitationSent = InAppNotificationType.Define("trainer.invitation.sent");
    public static readonly InAppNotificationType InvitationAccepted = InAppNotificationType.Define("trainer.invitation.accepted");
    public static readonly InAppNotificationType InvitationRejected = InAppNotificationType.Define("trainer.invitation.rejected");

    public static IReadOnlyCollection<InAppNotificationType> All { get; } =
    [
        InvitationSent,
        InvitationAccepted,
        InvitationRejected
    ];

    public static bool TryFromValue(string? value, out InAppNotificationType notificationType)
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
