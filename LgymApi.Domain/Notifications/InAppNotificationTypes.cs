namespace LgymApi.Domain.Notifications;

public static class InAppNotificationTypes
{
    public static readonly InAppNotificationType InvitationSent = InAppNotificationType.Define("trainer.invitation.sent");
    public static readonly InAppNotificationType InvitationAccepted = InAppNotificationType.Define("trainer.invitation.accepted");
    public static readonly InAppNotificationType InvitationRejected = InAppNotificationType.Define("trainer.invitation.rejected");
    public static readonly InAppNotificationType ReportSubmissionReceived = InAppNotificationType.Define("ReportSubmissionReceived");
    public static readonly InAppNotificationType TrainerRelationshipEnded = InAppNotificationType.Define("TrainerRelationshipEnded");
    // Keep this value aligned with the existing mobile notification contract.
    public static readonly InAppNotificationType ReportRequestReceived = InAppNotificationType.Define("ReportRequestReceived");
    public static readonly InAppNotificationType ReportFeedbackReceived = InAppNotificationType.Define("ReportFeedbackReceived");
    public static readonly InAppNotificationType DietPlanUpdated = InAppNotificationType.Define("DietPlanUpdated");
    public static readonly InAppNotificationType TraineeNoteUpdated = InAppNotificationType.Define("TraineeNoteUpdated");

    public static IReadOnlyCollection<InAppNotificationType> All { get; } =
    [
        InvitationSent,
        InvitationAccepted,
        InvitationRejected,
        ReportSubmissionReceived,
        TrainerRelationshipEnded,
        ReportRequestReceived,
        ReportFeedbackReceived,
        DietPlanUpdated,
        TraineeNoteUpdated
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
