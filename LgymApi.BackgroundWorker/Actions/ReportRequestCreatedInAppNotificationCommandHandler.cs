using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Notifications;
using Microsoft.Extensions.Logging;
using NotificationsApp = global::LgymApi.Application.Notifications;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class ReportRequestCreatedInAppNotificationCommandHandler : global::LgymApi.BackgroundWorker.Common.IBackgroundAction<ReportRequestCreatedInAppNotificationCommand>
{
    private readonly NotificationsApp.IInAppNotificationService _notificationService;
    private readonly IUserRepository _userRepository;
    private readonly AppDefaultsOptions _appDefaultsOptions;
    private readonly ILogger<ReportRequestCreatedInAppNotificationCommandHandler> _logger;

    public ReportRequestCreatedInAppNotificationCommandHandler(
        NotificationsApp.IInAppNotificationService notificationService,
        IUserRepository userRepository,
        AppDefaultsOptions appDefaultsOptions,
        ILogger<ReportRequestCreatedInAppNotificationCommandHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _appDefaultsOptions = appDefaultsOptions ?? throw new ArgumentNullException(nameof(appDefaultsOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(ReportRequestCreatedInAppNotificationCommand command, CancellationToken cancellationToken = default)
    {
        await LocalizedReportNotificationDispatcher.DispatchAsync(
            _notificationService,
            _userRepository,
            _appDefaultsOptions,
            _logger,
            command.TraineeId,
            command.TrainerId,
            command.TemplateName,
            $"report-request:{command.RequestId}:created",
            $"/trainer/report-requests/{command.RequestId}",
            InAppNotificationTypes.ReportRequestReceived,
            () => global::LgymApi.Resources.Messages.TrainerReportRequestReceived,
            "report-request",
            cancellationToken);
    }
}
