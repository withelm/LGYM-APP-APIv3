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

public sealed class InvitationRevokedEmailHandler : IBackgroundAction<InvitationRevokedCommand>
{
    private readonly ITrainerRelationshipRepository _invitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailScheduler<InvitationRevokedEmailPayload> _emailScheduler;
    private readonly ILogger<InvitationRevokedEmailHandler> _logger;
    private readonly IEmailNotificationsFeature _emailNotificationsFeature;
    private readonly AppDefaultsOptions _appDefaultsOptions;

    public InvitationRevokedEmailHandler(
        ITrainerRelationshipRepository invitationRepository,
        IUserRepository userRepository,
        IEmailScheduler<InvitationRevokedEmailPayload> emailScheduler,
        IEmailNotificationsFeature emailNotificationsFeature,
        ILogger<InvitationRevokedEmailHandler> logger,
        AppDefaultsOptions appDefaultsOptions)
    {
        _invitationRepository = invitationRepository ?? throw new ArgumentNullException(nameof(invitationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _emailScheduler = emailScheduler ?? throw new ArgumentNullException(nameof(emailScheduler));
        _emailNotificationsFeature = emailNotificationsFeature ?? throw new ArgumentNullException(nameof(emailNotificationsFeature));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _appDefaultsOptions = appDefaultsOptions ?? throw new ArgumentNullException(nameof(appDefaultsOptions));
    }

    public async Task ExecuteAsync(InvitationRevokedCommand command, CancellationToken cancellationToken = default)
    {
        if (!_emailNotificationsFeature.Enabled)
        {
            return;
        }

        var invitation = await _invitationRepository.FindInvitationByIdAsync(command.InvitationId, cancellationToken);
        if (invitation == null)
        {
            _logger.LogWarning("Invitation not found for InvitationRevoked {InvitationId}", command.InvitationId);
            return;
        }

        var trainer = await _userRepository.FindByIdAsync((Id<User>)invitation.TrainerId, cancellationToken);
        if (trainer == null)
        {
            _logger.LogWarning("Trainer user not found for InvitationRevoked {InvitationId}, TrainerId {TrainerId}", command.InvitationId, invitation.TrainerId);
            return;
        }

        if (string.IsNullOrWhiteSpace(invitation.InviteeEmail))
        {
            _logger.LogWarning("InvitationRevoked email skipped for Invitation {InvitationId} - no invitee email provided", command.InvitationId);
            return;
        }

        var emailPayload = new InvitationRevokedEmailPayload
        {
            InvitationId = command.InvitationId,
            TrainerName = trainer.Name,
            RecipientEmail = invitation.InviteeEmail,
            CultureName = _appDefaultsOptions.PreferredLanguage,
            PreferredTimeZone = _appDefaultsOptions.PreferredTimeZone
        };

        await _emailScheduler.ScheduleAsync(emailPayload, cancellationToken);

        _logger.LogInformation("InvitationRevoked email scheduled for Invitation {InvitationId} to {Email}", command.InvitationId, invitation.InviteeEmail);
    }
}
