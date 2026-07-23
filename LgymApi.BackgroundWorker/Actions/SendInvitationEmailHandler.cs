using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Coaching.Contracts.Notifications;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Notifications.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

public sealed partial class SendInvitationEmailHandler : global::LgymApi.BackgroundWorker.Actions.Contracts.IBackgroundAction<InvitationCreatedCommand>
{
    private readonly ICoachingNotificationReadService _notificationReadService;
    private readonly IAccountReadService _accountReadService;
    private readonly ICoachingNotificationIntentService _notificationIntentService;
    private readonly ICoachingEmailNotificationScheduler _emailScheduler;
    private readonly ILogger<SendInvitationEmailHandler> _logger;

    public SendInvitationEmailHandler(
        ICoachingNotificationReadService notificationReadService,
        IAccountReadService accountReadService,
        ICoachingNotificationIntentService notificationIntentService,
        ICoachingEmailNotificationScheduler emailScheduler,
        ILogger<SendInvitationEmailHandler> logger)
    {
        _notificationReadService = notificationReadService ?? throw new ArgumentNullException(nameof(notificationReadService));
        _accountReadService = accountReadService ?? throw new ArgumentNullException(nameof(accountReadService));
        _notificationIntentService = notificationIntentService ?? throw new ArgumentNullException(nameof(notificationIntentService));
        _emailScheduler = emailScheduler ?? throw new ArgumentNullException(nameof(emailScheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(InvitationCreatedCommand command, CancellationToken cancellationToken = default)
    {
        var invitation = await _notificationReadService.GetInvitationAsync(command.InvitationId, cancellationToken);
        if (invitation == null)
        {
            _logger.LogWarning("Invitation not found for Invitation {InvitationId}", command.InvitationId);
            return;
        }

        var trainer = await _accountReadService.GetByIdAsync(invitation.TrainerId, cancellationToken);
        if (trainer == null)
        {
            _logger.LogWarning("Trainer user not found for Invitation {InvitationId}, TrainerId {TrainerId}", command.InvitationId, invitation.TrainerId);
            return;
        }

        AccountReadModel? trainee = null;

        if (invitation.TraineeId.HasValue)
        {
            trainee = await _accountReadService.GetByIdAsync(invitation.TraineeId.Value, cancellationToken);
            if (trainee == null)
            {
                _logger.LogWarning("Trainee user not found for Invitation {InvitationId}, TraineeId {TraineeId}", command.InvitationId, invitation.TraineeId);
                return;
            }
        }

        var result = await _notificationIntentService.SubmitAsync(
            new InvitationCreatedCoachingNotificationIntent(
                CoachingNotificationLegacyChannel.Email,
                invitation.InvitationId,
                invitation.TrainerId,
                invitation.TraineeId,
                invitation.InviteeEmail,
                invitation.InvitationCode,
                invitation.ExpiresAt,
                trainer,
                trainee),
            cancellationToken);
        var schedulingRequest = result.EmailSchedulingRequest;
        if (schedulingRequest == null)
        {
            return;
        }

        await _emailScheduler.ScheduleAsync(schedulingRequest, cancellationToken);

        _logger.LogInformation("Invitation email scheduled for Invitation {InvitationId} to {Email}", command.InvitationId, schedulingRequest.RecipientEmail);
    }
}
