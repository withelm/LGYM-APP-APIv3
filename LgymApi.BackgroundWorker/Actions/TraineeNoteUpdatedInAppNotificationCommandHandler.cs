using System.Globalization;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class TraineeNoteUpdatedInAppNotificationCommandHandler : global::LgymApi.BackgroundWorker.Common.IBackgroundAction<TraineeNoteUpdatedInAppNotificationCommand>
{
    private readonly IInAppNotificationService _notificationService;
    private readonly IUserRepository _userRepository;
    private readonly AppDefaultsOptions _appDefaultsOptions;
    private readonly ILogger<TraineeNoteUpdatedInAppNotificationCommandHandler> _logger;

    public TraineeNoteUpdatedInAppNotificationCommandHandler(
        IInAppNotificationService notificationService,
        IUserRepository userRepository,
        AppDefaultsOptions appDefaultsOptions,
        ILogger<TraineeNoteUpdatedInAppNotificationCommandHandler> logger)
    {
        _notificationService = notificationService;
        _userRepository = userRepository;
        _appDefaultsOptions = appDefaultsOptions;
        _logger = logger;
    }

    public async Task ExecuteAsync(TraineeNoteUpdatedInAppNotificationCommand command, CancellationToken cancellationToken = default)
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
            var noteTitle = string.IsNullOrWhiteSpace(command.NoteTitle)
                ? global::LgymApi.Resources.Messages.GenericTrainerNoteDisplayName
                : command.NoteTitle!.Trim();

            var input = new CreateInAppNotificationInput(
                command.TraineeId,
                command.TrainerId,
                $"trainee-note:{command.TraineeNoteId}:{command.TriggeredAt:O}",
                false,
                string.Format(global::LgymApi.Resources.Messages.TrainerTraineeNoteUpdated, trainerName, noteTitle),
                $"/trainer/notes/{command.TraineeNoteId}",
                InAppNotificationTypes.TraineeNoteUpdated);

            var result = await _notificationService.CreateAsync(input, cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to create trainee note notification for trainee {TraineeId}: {Error}", command.TraineeId, result.Error);
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
