using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

public sealed partial class SendInvitationEmailHandler : global::LgymApi.BackgroundWorker.Actions.Contracts.IBackgroundAction<InvitationCreatedCommand>
{
    private readonly ITrainerRelationshipRepository _invitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailScheduler<InvitationEmailPayload> _emailScheduler;
    private readonly IEmailNotificationLogRepository _emailNotificationLogRepository;
    private readonly ILogger<SendInvitationEmailHandler> _logger;
    private readonly IEmailNotificationsFeature _emailNotificationsFeature;
    private readonly AppDefaultsOptions _appDefaultsOptions;

    public SendInvitationEmailHandler(
        ITrainerRelationshipRepository invitationRepository,
        IUserRepository userRepository,
        IEmailScheduler<InvitationEmailPayload> emailScheduler,
        IEmailNotificationLogRepository emailNotificationLogRepository,
        IEmailNotificationsFeature emailNotificationsFeature,
        ILogger<SendInvitationEmailHandler> logger,
        AppDefaultsOptions appDefaultsOptions)
    {
        _invitationRepository = invitationRepository ?? throw new ArgumentNullException(nameof(invitationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _emailScheduler = emailScheduler ?? throw new ArgumentNullException(nameof(emailScheduler));
        _emailNotificationLogRepository = emailNotificationLogRepository ?? throw new ArgumentNullException(nameof(emailNotificationLogRepository));
        _emailNotificationsFeature = emailNotificationsFeature ?? throw new ArgumentNullException(nameof(emailNotificationsFeature));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _appDefaultsOptions = appDefaultsOptions ?? throw new ArgumentNullException(nameof(appDefaultsOptions));
    }

    public async Task ExecuteAsync(InvitationCreatedCommand command, CancellationToken cancellationToken = default)
    {
        if (!_emailNotificationsFeature.Enabled)
        {
            return;
        }

        var invitation = await _invitationRepository.FindInvitationByIdAsync(command.InvitationId, cancellationToken);
        if (invitation == null)
        {
            _logger.LogWarning("Invitation not found for Invitation {InvitationId}", command.InvitationId);
            return;
        }

        var trainer = await _userRepository.FindByIdAsync((Id<User>)invitation.TrainerId, cancellationToken);
        if (trainer == null)
        {
            _logger.LogWarning("Trainer user not found for Invitation {InvitationId}, TrainerId {TrainerId}", command.InvitationId, invitation.TrainerId);
            return;
        }

        string recipientEmail;
        string preferredTimeZone;

        if (invitation.TraineeId.HasValue)
        {
            var trainee = await _userRepository.FindByIdAsync(invitation.TraineeId.Value, cancellationToken);
            if (trainee == null)
            {
                _logger.LogWarning("Trainee user not found for Invitation {InvitationId}, TraineeId {TraineeId}", command.InvitationId, invitation.TraineeId);
                return;
            }

            recipientEmail = trainee.Email;
            preferredTimeZone = string.IsNullOrWhiteSpace(trainee.PreferredTimeZone) ? _appDefaultsOptions.PreferredTimeZone : trainee.PreferredTimeZone;
        }
        else
        {
            recipientEmail = invitation.InviteeEmail;
            preferredTimeZone = _appDefaultsOptions.PreferredTimeZone;
        }

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            _logger.LogWarning("Invitation email skipped for Invitation {InvitationId} - no recipient email provided", command.InvitationId);
            return;
        }

        // Idempotency check: query existing notification before scheduling
        var existingNotification = await _emailNotificationLogRepository.FindByCorrelationAsync(
            EmailNotificationTypes.TrainerInvitation,
            command.InvitationId.Rebind<CorrelationScope>(),
            recipientEmail,
            cancellationToken);

        if (existingNotification != null)
        {
            if (existingNotification.Status != EmailNotificationStatus.Failed)
            {
                // Return early if already sent or in any non-failed status
                _logger.LogInformation(
                    "Invitation email already processed for Invitation {InvitationId} (Status: {Status})",
                    command.InvitationId, existingNotification.Status);
                return;
            }

            // Only proceed if failed but retries remain
            if (existingNotification.Attempts >= 5)
            {
                _logger.LogInformation(
                    "Invitation email max retries reached for Invitation {InvitationId}",
                    command.InvitationId);
                return;
            }
        }

        var emailPayload = new InvitationEmailPayload
        {
            InvitationId = command.InvitationId,
            InvitationCode = invitation.Code,
            ExpiresAt = invitation.ExpiresAt,
            TrainerName = trainer.Name,
            RecipientEmail = recipientEmail,
            CultureName = string.IsNullOrWhiteSpace(trainer.PreferredLanguage) ? _appDefaultsOptions.PreferredLanguage : trainer.PreferredLanguage,
            PreferredTimeZone = preferredTimeZone
        };

        await _emailScheduler.ScheduleAsync(emailPayload, cancellationToken);

        _logger.LogInformation("Invitation email scheduled for Invitation {InvitationId} to {Email}", command.InvitationId, recipientEmail);
    }
}
