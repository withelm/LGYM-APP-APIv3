using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Notifications;
using Microsoft.Extensions.Logging;
using System.Globalization;
using NotificationsApp = global::LgymApi.Application.Notifications;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class TrainerRelationshipEndedInAppNotificationCommandHandler : global::LgymApi.BackgroundWorker.Common.IBackgroundAction<TrainerRelationshipEndedInAppNotificationCommand>
{
    private readonly NotificationsApp.IInAppNotificationService _notificationService;
    private readonly IUserRepository _userRepository;
    private readonly AppDefaultsOptions _appDefaultsOptions;
    private readonly ILogger<TrainerRelationshipEndedInAppNotificationCommandHandler> _logger;

    public TrainerRelationshipEndedInAppNotificationCommandHandler(
        NotificationsApp.IInAppNotificationService notificationService,
        IUserRepository userRepository,
        AppDefaultsOptions appDefaultsOptions,
        ILogger<TrainerRelationshipEndedInAppNotificationCommandHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _appDefaultsOptions = appDefaultsOptions ?? throw new ArgumentNullException(nameof(appDefaultsOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(TrainerRelationshipEndedInAppNotificationCommand command, CancellationToken cancellationToken = default)
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

            var input = new NotificationsApp.Models.CreateInAppNotificationInput(
                command.TrainerId,
                command.TraineeId,
                $"trainer-relationship-ended:{command.TrainerId}:{command.TraineeId}",
                false,
                string.Format(global::LgymApi.Resources.Messages.TrainerRelationshipEnded, traineeName),
                "/trainer/members",
                InAppNotificationTypes.TrainerRelationshipEnded);

            var result = await _notificationService.CreateAsync(input, cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogError($"Failed to create trainer-relationship-ended notification for trainer {command.TrainerId}: {result.Error}");
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
