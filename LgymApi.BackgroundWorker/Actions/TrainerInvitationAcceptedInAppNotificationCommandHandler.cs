using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.BackgroundWorker.Actions.Contracts;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

public sealed partial class TrainerInvitationAcceptedInAppNotificationCommandHandler : IBackgroundAction<TrainerInvitationAcceptedInAppNotificationCommand>
{
    private readonly ICoachingNotificationIntentService _notificationIntentService;
    private readonly ILogger<TrainerInvitationAcceptedInAppNotificationCommandHandler> _logger;

    public TrainerInvitationAcceptedInAppNotificationCommandHandler(
        ICoachingNotificationIntentService notificationIntentService,
        ILogger<TrainerInvitationAcceptedInAppNotificationCommandHandler> logger)
    {
        _notificationIntentService = notificationIntentService ?? throw new ArgumentNullException(nameof(notificationIntentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(TrainerInvitationAcceptedInAppNotificationCommand command, CancellationToken cancellationToken = default)
    {
        var result = await _notificationIntentService.SubmitAsync(
            new InvitationAcceptedCoachingNotificationIntent(
                CoachingNotificationLegacyChannel.InApp,
                command.InvitationId,
                command.TrainerId,
                command.TraineeId,
                null,
                null),
            cancellationToken);

        if (result.InAppError is not null)
        {
            _logger.LogError(
                "Failed to create invitation-accepted notification for trainer {TrainerId}: {Error}",
                command.TrainerId,
                result.InAppError);
        }
    }
}
