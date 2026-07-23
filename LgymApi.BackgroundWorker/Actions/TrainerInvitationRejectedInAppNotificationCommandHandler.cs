using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.BackgroundWorker.Actions.Contracts;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

public sealed partial class TrainerInvitationRejectedInAppNotificationCommandHandler : IBackgroundAction<TrainerInvitationRejectedInAppNotificationCommand>
{
    private readonly ICoachingNotificationIntentService _notificationIntentService;
    private readonly ILogger<TrainerInvitationRejectedInAppNotificationCommandHandler> _logger;

    public TrainerInvitationRejectedInAppNotificationCommandHandler(
        ICoachingNotificationIntentService notificationIntentService,
        ILogger<TrainerInvitationRejectedInAppNotificationCommandHandler> logger)
    {
        _notificationIntentService = notificationIntentService ?? throw new ArgumentNullException(nameof(notificationIntentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(TrainerInvitationRejectedInAppNotificationCommand command, CancellationToken cancellationToken = default)
    {
        var result = await _notificationIntentService.SubmitAsync(
            new InvitationRejectedCoachingNotificationIntent(
                CoachingNotificationLegacyChannel.InApp,
                command.InvitationId,
                command.TrainerId,
                command.TraineeId),
            cancellationToken);

        if (result.InAppError is not null)
        {
            _logger.LogError(
                "Failed to create invitation-rejected notification for trainer {TrainerId}: {Error}",
                command.TrainerId,
                result.InAppError);
        }
    }
}
