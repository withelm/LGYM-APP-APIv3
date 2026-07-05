using System.Globalization;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Notifications;
using Microsoft.Extensions.Logging;
using NotificationsApp = global::LgymApi.Application.Notifications;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class DietPlanUpdatedInAppNotificationCommandHandler : global::LgymApi.BackgroundWorker.Common.IBackgroundAction<DietPlanUpdatedInAppNotificationCommand>
{
    private readonly NotificationsApp.IInAppNotificationService _notificationService;
    private readonly IUserRepository _userRepository;
    private readonly AppDefaultsOptions _appDefaultsOptions;
    private readonly ILogger<DietPlanUpdatedInAppNotificationCommandHandler> _logger;

    public DietPlanUpdatedInAppNotificationCommandHandler(
        NotificationsApp.IInAppNotificationService notificationService,
        IUserRepository userRepository,
        AppDefaultsOptions appDefaultsOptions,
        ILogger<DietPlanUpdatedInAppNotificationCommandHandler> logger)
    {
        _notificationService = notificationService;
        _userRepository = userRepository;
        _appDefaultsOptions = appDefaultsOptions;
        _logger = logger;
    }

    public async Task ExecuteAsync(DietPlanUpdatedInAppNotificationCommand command, CancellationToken cancellationToken = default)
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
            var planName = string.IsNullOrWhiteSpace(command.DietPlanName)
                ? global::LgymApi.Resources.Messages.GenericDietPlanDisplayName
                : command.DietPlanName.Trim();

            var input = new NotificationsApp.Models.CreateInAppNotificationInput(
                command.TraineeId,
                command.TrainerId,
                $"diet-plan:{command.DietPlanId}:{command.TriggeredAt:O}",
                false,
                string.Format(global::LgymApi.Resources.Messages.TrainerDietPlanUpdated, trainerName, planName),
                $"/trainer/diet-plans/{command.DietPlanId}",
                InAppNotificationTypes.DietPlanUpdated);

            var result = await _notificationService.CreateAsync(input, cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to create diet-plan notification for trainee {TraineeId}: {Error}", command.TraineeId, result.Error);
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
