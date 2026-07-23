using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Coaching.Contracts.Notifications;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.BackgroundWorker.Actions.Contracts;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

public sealed partial class TrainerInvitationCreatedInAppNotificationCommandHandler : IBackgroundAction<TrainerInvitationCreatedInAppNotificationCommand>
{
    private readonly ICoachingNotificationIntentService _notificationIntentService;
    private readonly ICoachingNotificationReadService _notificationReadService;
    private readonly IAccountReadService _accountReadService;
    private readonly ILogger<TrainerInvitationCreatedInAppNotificationCommandHandler> _logger;

    public TrainerInvitationCreatedInAppNotificationCommandHandler(
        ICoachingNotificationIntentService notificationIntentService,
        ICoachingNotificationReadService notificationReadService,
        IAccountReadService accountReadService,
        ILogger<TrainerInvitationCreatedInAppNotificationCommandHandler> logger)
    {
        _notificationIntentService = notificationIntentService ?? throw new ArgumentNullException(nameof(notificationIntentService));
        _notificationReadService = notificationReadService ?? throw new ArgumentNullException(nameof(notificationReadService));
        _accountReadService = accountReadService ?? throw new ArgumentNullException(nameof(accountReadService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(TrainerInvitationCreatedInAppNotificationCommand command, CancellationToken cancellationToken = default)
    {
        var invitation = await _notificationReadService.GetInvitationAsync(command.InvitationId, cancellationToken);
        if (invitation is null)
        {
            return;
        }

        var trainer = await _accountReadService.GetByIdAsync(command.TrainerId, cancellationToken);
        var result = await _notificationIntentService.SubmitAsync(
            new InvitationCreatedCoachingNotificationIntent(
                CoachingNotificationLegacyChannel.InApp,
                command.InvitationId,
                command.TrainerId,
                command.TraineeId,
                invitation.InviteeEmail,
                invitation.InvitationCode,
                invitation.ExpiresAt,
                trainer,
                null),
            cancellationToken);

        if (result.InAppError is not null)
        {
            _logger.LogError(
                "Failed to create invitation-sent notification for trainee {TraineeId}: {Error}",
                command.TraineeId,
                result.InAppError);
        }
    }
}
