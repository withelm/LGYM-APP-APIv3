using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.BackgroundWorker.Actions.Contracts;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

public sealed partial class TrainerRelationshipEndedInAppNotificationCommandHandler : IBackgroundAction<TrainerRelationshipEndedInAppNotificationCommand>
{
    private readonly ICoachingNotificationIntentService _notificationIntentService;
    private readonly IAccountReadService _accountReadService;
    private readonly ILogger<TrainerRelationshipEndedInAppNotificationCommandHandler> _logger;

    public TrainerRelationshipEndedInAppNotificationCommandHandler(
        ICoachingNotificationIntentService notificationIntentService,
        IAccountReadService accountReadService,
        ILogger<TrainerRelationshipEndedInAppNotificationCommandHandler> logger)
    {
        _notificationIntentService = notificationIntentService ?? throw new ArgumentNullException(nameof(notificationIntentService));
        _accountReadService = accountReadService ?? throw new ArgumentNullException(nameof(accountReadService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(TrainerRelationshipEndedInAppNotificationCommand command, CancellationToken cancellationToken = default)
    {
        var trainee = await _accountReadService.GetByIdAsync(command.TraineeId, cancellationToken);
        var trainer = await _accountReadService.GetByIdAsync(command.TrainerId, cancellationToken);
        var result = await _notificationIntentService.SubmitAsync(
            new RelationshipEndedCoachingNotificationIntent(
                CoachingNotificationLegacyChannel.InApp,
                command.TrainerId,
                command.TraineeId,
                trainer,
                trainee),
            cancellationToken);

        if (result.InAppError is not null)
        {
            _logger.LogError($"Failed to create trainer-relationship-ended notification for trainer {command.TrainerId}: {result.InAppError}");
        }
    }
}
