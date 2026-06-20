using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Notifications;
using Microsoft.Extensions.Logging;
using System.Globalization;
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
        var trainer = await _userRepository.FindByIdAsync(command.TrainerId, cancellationToken);
        var trainee = await _userRepository.FindByIdAsync(command.TraineeId, cancellationToken);
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = ResolveCulture(trainee?.PreferredLanguage);
            var trainerName = string.IsNullOrWhiteSpace(trainer?.Name)
                ? global::LgymApi.Resources.Messages.GenericTrainerDisplayName
                : trainer.Name;
            var templateName = string.IsNullOrWhiteSpace(command.TemplateName)
                ? global::LgymApi.Resources.Messages.GenericReportDisplayName
                : command.TemplateName.Trim();

            var input = new NotificationsApp.Models.CreateInAppNotificationInput(
                command.TraineeId,
                command.TrainerId,
                $"report-feedback:{command.SubmissionId}:{command.TriggeredAt:O}",
                false,
                string.Format(global::LgymApi.Resources.Messages.TrainerReportFeedbackReceived, trainerName, templateName),
                $"/trainer/report-submissions/{command.SubmissionId}",
                InAppNotificationTypes.ReportFeedbackReceived);

            var result = await _notificationService.CreateAsync(input, cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogError($"Failed to create report-feedback notification for trainee {command.TraineeId}: {result.Error}");
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
