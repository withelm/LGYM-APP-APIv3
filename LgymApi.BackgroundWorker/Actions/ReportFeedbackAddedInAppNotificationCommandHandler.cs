using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Notifications;
using Microsoft.Extensions.Logging;
using NotificationsApp = global::LgymApi.Application.Notifications;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class ReportFeedbackAddedInAppNotificationCommandHandler : global::LgymApi.BackgroundWorker.Common.IBackgroundAction<ReportFeedbackAddedInAppNotificationCommand>
{
    private readonly NotificationsApp.IInAppNotificationService _notificationService;
    private readonly IUserRepository _userRepository;
    private readonly AppDefaultsOptions _appDefaultsOptions;
    private readonly ILogger<ReportFeedbackAddedInAppNotificationCommandHandler> _logger;

    public ReportFeedbackAddedInAppNotificationCommandHandler(
        NotificationsApp.IInAppNotificationService notificationService,
        IUserRepository userRepository,
        AppDefaultsOptions appDefaultsOptions,
        ILogger<ReportFeedbackAddedInAppNotificationCommandHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _appDefaultsOptions = appDefaultsOptions ?? throw new ArgumentNullException(nameof(appDefaultsOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(ReportFeedbackAddedInAppNotificationCommand command, CancellationToken cancellationToken = default)
    {
        await LocalizedReportNotificationDispatcher.DispatchAsync(
            _notificationService,
            _userRepository,
            _appDefaultsOptions,
            _logger,
            command.TraineeId,
            command.TrainerId,
            command.TemplateName,
            $"report-feedback:{command.SubmissionId}:{command.TriggeredAt:O}",
            $"/trainer/report-submissions/{command.SubmissionId}",
            InAppNotificationTypes.ReportFeedbackReceived,
            () => global::LgymApi.Resources.Messages.TrainerReportFeedbackReceived,
            "report-feedback",
            cancellationToken);
    }
}
