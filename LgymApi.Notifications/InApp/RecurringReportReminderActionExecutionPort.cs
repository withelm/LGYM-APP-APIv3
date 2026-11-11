using System.Globalization;
using System.Text.Json;
using LgymApi.Application.Notifications.Contracts.InApp;
using LgymApi.Application.Options;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Resources;

namespace LgymApi.Application.Notifications.Contracts.InApp
{
    public interface IRecurringReportReminderActionExecutionPort
    {
        Task ExecuteAsync(string payloadJson, CancellationToken cancellationToken = default);
    }
}

namespace LgymApi.Application.Notifications.InApp
{
    internal sealed class RecurringReportReminderActionExecutionPort(
        IInAppNotificationWireWriter notificationWriter,
        IAccountLookupService accountLookupService,
        AppDefaultsOptions defaults) : IRecurringReportReminderActionExecutionPort
    {
        public async Task ExecuteAsync(string payloadJson, CancellationToken cancellationToken = default)
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;
            var isUpcoming = root.GetProperty("isUpcomingRequestReminder").GetBoolean();
            var assignmentId = root.GetProperty("assignmentId").GetString() ?? string.Empty;
            var scheduleRevision = root.GetProperty("scheduleRevision").GetInt32();
            var requestId = root.TryGetProperty("reportRequestId", out var request)
                && request.ValueKind != JsonValueKind.Null
                ? request.GetString() ?? string.Empty
                : string.Empty;

            await ReportNotificationActionHelpers.ExecuteWithParticipantsAsync(
                root,
                "traineeId",
                "trainerId",
                "Recurring report reminder",
                () => Messages.GenericTrainerDisplayName,
                accountLookupService,
                defaults,
                async (traineeId, trainerId, _) =>
                {
                    var deliveryKey = isUpcoming
                        ? $"report-recurring-reminder:upcoming:{assignmentId}:{scheduleRevision}"
                        : $"report-recurring-reminder:unanswered:{requestId}";
                    var message = isUpcoming
                        ? string.Format(
                            Messages.TraineeRecurringReportRequestUpcoming,
                            root.GetProperty("scheduledFor").GetDateTimeOffset()
                                .ToString("g", CultureInfo.CurrentUICulture))
                        : Messages.TraineeRecurringReportRequestUnanswered;
                    var redirectUrl = isUpcoming
                        ? $"/trainer/members/{traineeId}?tab=reports"
                        : $"/trainer/report-requests/{requestId}";

                    await notificationWriter.CreateAsync(
                        traineeId.ToString(),
                        trainerId.ToString(),
                        deliveryKey,
                        message,
                        redirectUrl,
                        "ReportRequestReceived",
                        cancellationToken);
                },
                cancellationToken);
        }
    }
}
