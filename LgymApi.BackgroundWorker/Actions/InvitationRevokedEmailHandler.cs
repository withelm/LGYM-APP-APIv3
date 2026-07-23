using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Coaching.Contracts.Notifications;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Notifications.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

public sealed partial class InvitationRevokedEmailHandler : global::LgymApi.BackgroundWorker.Actions.Contracts.IBackgroundAction<InvitationRevokedCommand>
{
    private readonly ICoachingNotificationReadService _notificationReadService;
    private readonly IAccountReadService _accountReadService;
    private readonly ICoachingNotificationIntentService _notificationIntentService;
    private readonly ICoachingEmailNotificationScheduler _emailScheduler;
    private readonly ILogger<InvitationRevokedEmailHandler> _logger;

    public InvitationRevokedEmailHandler(
        ICoachingNotificationReadService notificationReadService,
        IAccountReadService accountReadService,
        ICoachingNotificationIntentService notificationIntentService,
        ICoachingEmailNotificationScheduler emailScheduler,
        ILogger<InvitationRevokedEmailHandler> logger)
    {
        _notificationReadService = notificationReadService ?? throw new ArgumentNullException(nameof(notificationReadService));
        _accountReadService = accountReadService ?? throw new ArgumentNullException(nameof(accountReadService));
        _notificationIntentService = notificationIntentService ?? throw new ArgumentNullException(nameof(notificationIntentService));
        _emailScheduler = emailScheduler ?? throw new ArgumentNullException(nameof(emailScheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(InvitationRevokedCommand command, CancellationToken cancellationToken = default)
    {
        var invitation = await _notificationReadService.GetInvitationAsync(command.InvitationId, cancellationToken);
        if (invitation == null)
        {
            _logger.LogWarning("Invitation not found for InvitationRevoked {InvitationId}", command.InvitationId);
            return;
        }

        var trainer = await _accountReadService.GetByIdAsync(invitation.TrainerId, cancellationToken);
        if (trainer == null)
        {
            _logger.LogWarning("Trainer user not found for InvitationRevoked {InvitationId}, TrainerId {TrainerId}", command.InvitationId, invitation.TrainerId);
            return;
        }

        var result = await _notificationIntentService.SubmitAsync(
            new InvitationRevokedCoachingNotificationIntent(
                CoachingNotificationLegacyChannel.Email,
                invitation.InvitationId,
                invitation.TrainerId,
                invitation.InviteeEmail,
                trainer),
            cancellationToken);
        var schedulingRequest = result.EmailSchedulingRequest;
        if (schedulingRequest == null)
        {
            return;
        }

        await _emailScheduler.ScheduleAsync(schedulingRequest, cancellationToken);

        _logger.LogInformation("InvitationRevoked email scheduled for Invitation {InvitationId} to {Email}", command.InvitationId, schedulingRequest.RecipientEmail);
    }
}
