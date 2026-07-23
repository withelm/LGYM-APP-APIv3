using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.BackgroundWorker.Actions.Contracts;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

public sealed partial class TraineeNoteUpdatedInAppNotificationCommandHandler : IBackgroundAction<TraineeNoteUpdatedInAppNotificationCommand>
{
    private readonly ICoachingNotificationIntentService _notificationIntentService;
    private readonly IAccountReadService _accountReadService;
    private readonly ILogger<TraineeNoteUpdatedInAppNotificationCommandHandler> _logger;

    public TraineeNoteUpdatedInAppNotificationCommandHandler(
        ICoachingNotificationIntentService notificationIntentService,
        IAccountReadService accountReadService,
        ILogger<TraineeNoteUpdatedInAppNotificationCommandHandler> logger)
    {
        _notificationIntentService = notificationIntentService ?? throw new ArgumentNullException(nameof(notificationIntentService));
        _accountReadService = accountReadService ?? throw new ArgumentNullException(nameof(accountReadService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(TraineeNoteUpdatedInAppNotificationCommand command, CancellationToken cancellationToken = default)
    {
        var trainer = await _accountReadService.GetByIdAsync(command.TrainerId, cancellationToken);
        var trainee = await _accountReadService.GetByIdAsync(command.TraineeId, cancellationToken);
        var result = await _notificationIntentService.SubmitAsync(
            new TraineeNoteUpdatedCoachingNotificationIntent(
                CoachingNotificationLegacyChannel.InApp,
                command.TraineeNoteId,
                command.TraineeId,
                command.TrainerId,
                command.NoteTitle,
                command.TriggeredAt,
                trainer,
                trainee),
            cancellationToken);

        if (result.InAppError is not null)
        {
            _logger.LogError("Failed to create trainee note notification for trainee {TraineeId}: {Error}", command.TraineeId, result.InAppError);
        }
    }
}
