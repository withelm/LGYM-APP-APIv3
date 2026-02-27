using LgymApi.Domain.Notifications;

namespace LgymApi.BackgroundWorker.Common.Notifications;

public static class EmailNotificationTypes
{
    public static EmailNotificationType Welcome => Domain.Notifications.EmailNotificationTypes.Welcome;
    public static EmailNotificationType TrainerInvitation => Domain.Notifications.EmailNotificationTypes.TrainerInvitation;
    public static EmailNotificationType TrainingCompleted => Domain.Notifications.EmailNotificationTypes.TrainingCompleted;
}
