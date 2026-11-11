using System.Globalization;
using System.Text.Json;
using LgymApi.Application.Notifications.Contracts.InApp;
using LgymApi.Application.Options;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Resources;

namespace LgymApi.Application.Notifications.Contracts.InApp
{
    public interface IReportFeedbackReadActionExecutionPort
    {
        Task ExecuteAsync(string payloadJson, CancellationToken cancellationToken = default);
    }
}

namespace LgymApi.Application.Notifications.InApp
{
internal sealed class ReportFeedbackReadActionExecutionPort(
    IInAppNotificationWireWriter notificationWriter,
    IAccountLookupService accountLookupService,
    AppDefaultsOptions defaults) : IReportFeedbackReadActionExecutionPort
{
    public async Task ExecuteAsync(string payloadJson, CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;
        var submissionId = root.GetProperty("submissionId").GetString() ?? string.Empty;
        await ReportNotificationActionHelpers.ExecuteWithParticipantsAsync(
            root,
            "trainerId",
            "traineeId",
            "Report feedback read",
            () => Messages.GenericTraineeDisplayName,
            accountLookupService,
            defaults,
            async (trainerId, traineeId, traineeName) =>
            {
                var hasScheduledDate = root.TryGetProperty("nextRecurringRequestAt", out var scheduledDate)
                    && scheduledDate.ValueKind != JsonValueKind.Null;
                var message = hasScheduledDate
                    ? string.Format(Messages.TraineeReportFeedbackReadScheduled, traineeName,
                        scheduledDate.GetDateTimeOffset().ToString("g", CultureInfo.CurrentUICulture))
                    : string.Format(Messages.TraineeReportFeedbackReadUnscheduled, traineeName);
                await notificationWriter.CreateAsync(
                    trainerId.ToString(),
                    traineeId.ToString(),
                    $"report-feedback-read:{submissionId}",
                    message,
                    $"/trainer/members/{traineeId}?tab=reports&submissionId={submissionId}",
                    "ReportFeedbackRead",
                    cancellationToken);
            },
            cancellationToken);
    }
}
}
