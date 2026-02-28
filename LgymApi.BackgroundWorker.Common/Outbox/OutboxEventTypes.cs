using LgymApi.Domain.Notifications;

namespace LgymApi.BackgroundWorker.Common.Outbox;

public static class OutboxEventTypes
{
    public static OutboxEventDefinition<EmailNotificationScheduledEvent> EmailNotificationScheduled { get; } =
        new(LgymApi.Domain.Notifications.OutboxEventTypes.EmailNotificationScheduled);
}
