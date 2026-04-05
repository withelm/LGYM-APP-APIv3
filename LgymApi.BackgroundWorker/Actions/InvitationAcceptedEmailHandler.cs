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

public sealed class InvitationAcceptedEmailHandler : IBackgroundAction<InvitationAcceptedCommand>
{
    private readonly ITrainerRelationshipRepository _invitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailScheduler<InvitationAcceptedEmailPayload> _emailScheduler;
    private readonly ILogger<InvitationAcceptedEmailHandler> _logger;
    private readonly IEmailNotificationsFeature _emailNotificationsFeature;
    private readonly AppDefaultsOptions _appDefaultsOptions;

    public InvitationAcceptedEmailHandler(
        ITrainerRelationshipRepository invitationRepository,
        IUserRepository userRepository,
        IEmailScheduler<InvitationAcceptedEmailPayload> emailScheduler,
        IEmailNotificationsFeature emailNotificationsFeature,
        ILogger<InvitationAcceptedEmailHandler> logger,
        AppDefaultsOptions appDefaultsOptions)
    {
        _invitationRepository = invitationRepository ?? throw new ArgumentNullException(nameof(invitationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _emailScheduler = emailScheduler ?? throw new ArgumentNullException(nameof(emailScheduler));
        _emailNotificationsFeature = emailNotificationsFeature ?? throw new ArgumentNullException(nameof(emailNotificationsFeature));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _appDefaultsOptions = appDefaultsOptions ?? throw new ArgumentNullException(nameof(appDefaultsOptions));
    }

    public async Task ExecuteAsync(InvitationAcceptedCommand command, CancellationToken cancellationToken = default)
    {
        if (!_emailNotificationsFeature.Enabled)
        {
            return;
        }

        var invitation = await _invitationRepository.FindInvitationByIdAsync(command.InvitationId, cancellationToken);
        if (invitation == null)
        {
            _logger.LogWarning("Invitation not found for InvitationAccepted {InvitationId}", command.InvitationId);
            return;
        }

        var trainer = await _userRepository.FindByIdAsync((Id<User>)invitation.TrainerId, cancellationToken);
        if (trainer == null)
        {
            _logger.LogWarning("Trainer user not found for InvitationAccepted {InvitationId}, TrainerId {TrainerId}", command.InvitationId, invitation.TrainerId);
            return;
        }

        if (!invitation.TraineeId.HasValue)
        {
            _logger.LogWarning("InvitationAccepted email skipped for Invitation {InvitationId} - TraineeId is null", command.InvitationId);
            return;
        }

        var trainee = await _userRepository.FindByIdAsync(invitation.TraineeId.Value, cancellationToken);
        if (trainee == null)
        {
            _logger.LogWarning("Trainee user not found for InvitationAccepted {InvitationId}, TraineeId {TraineeId}", command.InvitationId, invitation.TraineeId);
            return;
        }

        if (string.IsNullOrWhiteSpace(trainer.Email))
        {
            _logger.LogWarning("InvitationAccepted email skipped for Invitation {InvitationId} - no trainer email provided", command.InvitationId);
            return;
        }

        var emailPayload = new InvitationAcceptedEmailPayload
        {
            InvitationId = command.InvitationId,
            TrainerName = trainer.Name,
            TraineeName = trainee.Name,
            RecipientEmail = trainer.Email,
            CultureName = string.IsNullOrWhiteSpace(trainer.PreferredLanguage) ? _appDefaultsOptions.PreferredLanguage : trainer.PreferredLanguage,
            PreferredTimeZone = string.IsNullOrWhiteSpace(trainer.PreferredTimeZone) ? _appDefaultsOptions.PreferredTimeZone : trainer.PreferredTimeZone
        };

        await _emailScheduler.ScheduleAsync(emailPayload, cancellationToken);

        _logger.LogInformation("InvitationAccepted email scheduled for Invitation {InvitationId} to {Email}", command.InvitationId, trainer.Email);
    }
}
