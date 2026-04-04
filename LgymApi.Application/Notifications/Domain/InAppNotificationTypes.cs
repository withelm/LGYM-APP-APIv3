using System.Collections.ObjectModel;

namespace LgymApi.Notifications.Domain;

public static class InAppNotificationTypes
{
    public static readonly InAppNotificationType InvitationSent = InAppNotificationType.Define("trainer.invitation.sent");
    public static readonly InAppNotificationType InvitationAccepted = InAppNotificationType.Define("trainer.invitation.accepted");
    public static readonly InAppNotificationType InvitationRejected = InAppNotificationType.Define("trainer.invitation.rejected");

    public static IReadOnlyCollection<InAppNotificationType> All { get; } = new ReadOnlyCollection<InAppNotificationType>(
    [
        InvitationSent,
        InvitationAccepted,
        InvitationRejected
    ]);

    public static bool TryFromValue(string? value, out InAppNotificationType notificationType)
    {
        notificationType = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var type in All)
        {
            if (type.Value == value)
            {
                notificationType = type;
                return true;
            }
        }

        return false;
    }
}
