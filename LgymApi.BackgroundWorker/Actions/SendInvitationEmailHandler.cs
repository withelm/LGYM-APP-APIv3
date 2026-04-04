using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class SendInvitationEmailHandler : IBackgroundAction<InvitationCreatedCommand>
{
    private readonly ITrainerRelationshipRepository _invitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailScheduler<InvitationEmailPayload> _emailScheduler;
    private readonly ILogger<SendInvitationEmailHandler> _logger;
    private readonly IEmailNotificationsFeature _emailNotificationsFeature;
    private readonly AppDefaultsOptions _appDefaultsOptions;

    public SendInvitationEmailHandler(
        ITrainerRelationshipRepository invitationRepository,
        IUserRepository userRepository,
        IEmailScheduler<InvitationEmailPayload> emailScheduler,
        IEmailNotificationsFeature emailNotificationsFeature,
        ILogger<SendInvitationEmailHandler> logger,
        AppDefaultsOptions appDefaultsOptions)
    {
        _invitationRepository = invitationRepository ?? throw new ArgumentNullException(nameof(invitationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _emailScheduler = emailScheduler ?? throw new ArgumentNullException(nameof(emailScheduler));
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

        var trainee = await _userRepository.FindByIdAsync((Id<User>)invitation.TraineeId, cancellationToken);
        if (trainee == null)
        {
            _logger.LogWarning("Trainee user not found for Invitation {InvitationId}, TraineeId {TraineeId}", command.InvitationId, invitation.TraineeId);
            return;
        }

        var trainer = await _userRepository.FindByIdAsync((Id<User>)invitation.TrainerId, cancellationToken);
        if (trainer == null)
        {
            _logger.LogWarning("Trainer user not found for Invitation {InvitationId}, TrainerId {TrainerId}", command.InvitationId, invitation.TrainerId);
            return;
        }

        if (string.IsNullOrWhiteSpace(trainee.Email))
        {
            _logger.LogWarning("Invitation email skipped for Invitation {InvitationId} - no recipient email provided", command.InvitationId);
            return;
        }

        var emailPayload = new InvitationEmailPayload
        {
            InvitationId = command.InvitationId,
            InvitationCode = invitation.Code,
            ExpiresAt = invitation.ExpiresAt,
            TrainerName = trainer.Name,
            RecipientEmail = trainee.Email,
            CultureName = string.IsNullOrWhiteSpace(trainer.PreferredLanguage) ? _appDefaultsOptions.PreferredLanguage : trainer.PreferredLanguage,
            PreferredTimeZone = string.IsNullOrWhiteSpace(trainee.PreferredTimeZone) ? _appDefaultsOptions.PreferredTimeZone : trainee.PreferredTimeZone
        };

        await _emailScheduler.ScheduleAsync(emailPayload, cancellationToken);

        _logger.LogInformation("Invitation email scheduled for Invitation {InvitationId} to {Email}", command.InvitationId, trainee.Email);
    }
}
