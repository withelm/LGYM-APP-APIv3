using System.Text.Json;
using LgymApi.Application.Notifications.Contracts.InApp;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.BackgroundWorker.Actions.Contracts;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class RecurringReportReminderInAppNotificationCommandHandler(
    IRecurringReportReminderActionExecutionPort port) : IBackgroundAction<RecurringReportReminderInAppNotificationCommand>
{
    public Task ExecuteAsync(RecurringReportReminderInAppNotificationCommand command, CancellationToken cancellationToken = default) =>
        port.ExecuteAsync(JsonSerializer.Serialize(command, SharedSerializationOptions.Current), cancellationToken);
}
