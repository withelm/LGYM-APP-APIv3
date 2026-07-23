using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.BackgroundWorker.Actions.Contracts;
using LgymApi.Domain.Notifications;
using Microsoft.Extensions.Logging;
using System.Globalization;
using NotificationsApp = global::LgymApi.Application.Notifications;

namespace LgymApi.BackgroundWorker.Actions;

public sealed partial class ReportSubmissionCreatedInAppNotificationCommandHandler : IBackgroundAction<ReportSubmissionCreatedInAppNotificationCommand>
{
    private readonly NotificationsApp.IInAppNotificationService _notificationService;
    private readonly IUserRepository _userRepository;
    private readonly AppDefaultsOptions _appDefaultsOptions;
    private readonly ILogger<ReportSubmissionCreatedInAppNotificationCommandHandler> _logger;

    public ReportSubmissionCreatedInAppNotificationCommandHandler(
        NotificationsApp.IInAppNotificationService notificationService,
        IUserRepository userRepository,
        AppDefaultsOptions appDefaultsOptions,
        ILogger<ReportSubmissionCreatedInAppNotificationCommandHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _appDefaultsOptions = appDefaultsOptions ?? throw new ArgumentNullException(nameof(appDefaultsOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(ReportSubmissionCreatedInAppNotificationCommand command, CancellationToken cancellationToken = default)
    {
        var trainee = await _userRepository.FindByIdAsync(command.TraineeId, cancellationToken);
        var trainer = await _userRepository.FindByIdAsync(command.TrainerId, cancellationToken);
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = ResolveCulture(trainer?.PreferredLanguage);
            var traineeName = string.IsNullOrWhiteSpace(trainee?.Name)
                ? global::LgymApi.Resources.Messages.GenericTraineeDisplayName
                : trainee.Name;
            var templateName = string.IsNullOrWhiteSpace(command.TemplateName)
                ? global::LgymApi.Resources.Messages.GenericReportDisplayName
                : command.TemplateName.Trim();

            var input = new NotificationsApp.Models.CreateInAppNotificationInput(
                command.TrainerId,
                command.TraineeId,
                $"report-submission:{command.SubmissionId}",
                false,
                string.Format(global::LgymApi.Resources.Messages.TrainerReportSubmissionReceived, traineeName, templateName),
                $"/trainer/members/{command.TraineeId}?tab=reports&submissionId={command.SubmissionId}",
                InAppNotificationTypes.ReportSubmissionReceived);

            var result = await _notificationService.CreateAsync(input, cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogError($"Failed to create report-submission notification for trainer {command.TrainerId}: {result.Error}");
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private CultureInfo ResolveCulture(string? preferredLanguage)
    {
        var cultureName = string.IsNullOrWhiteSpace(preferredLanguage)
            ? _appDefaultsOptions.PreferredLanguage
            : preferredLanguage;

        try
        {
            return CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(_appDefaultsOptions.PreferredLanguage);
        }
    }
}
