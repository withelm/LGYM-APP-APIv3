using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

/// <summary>
/// Background action handler that schedules invitation email notifications.
/// Triggered when a trainer creates an invitation for a trainee.
/// </summary>
public sealed class SendInvitationEmailHandler : IBackgroundAction<InvitationCreatedCommand>
{
    private readonly ITrainerRelationshipRepository _invitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailScheduler<InvitationEmailPayload> _emailScheduler;
    private readonly ILogger<SendInvitationEmailHandler> _logger;

    public SendInvitationEmailHandler(
        ITrainerRelationshipRepository invitationRepository,
        IUserRepository userRepository,
        IEmailScheduler<InvitationEmailPayload> emailScheduler,
        ILogger<SendInvitationEmailHandler> logger)
    {
        _invitationRepository = invitationRepository ?? throw new ArgumentNullException(nameof(invitationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _emailScheduler = emailScheduler ?? throw new ArgumentNullException(nameof(emailScheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(InvitationCreatedCommand command, CancellationToken cancellationToken = default)
    {
        // Fetch invitation by ID
        var invitation = await _invitationRepository.FindInvitationByIdAsync(command.InvitationId, cancellationToken);
        if (invitation == null)
        {
            _logger.LogWarning(
                "Invitation not found for Invitation {InvitationId}",
                command.InvitationId);
            return;
        }

        // Fetch trainee email and language
        var trainee = await _userRepository.FindByIdAsync(invitation.TraineeId, cancellationToken);
        if (trainee == null)
        {
            _logger.LogWarning(
                "Trainee user not found for Invitation {InvitationId}, TraineeId {TraineeId}",
                command.InvitationId,
                invitation.TraineeId);
            return;
        }

        // Fetch trainer name and preferred language
        var trainer = await _userRepository.FindByIdAsync(invitation.TrainerId, cancellationToken);
        if (trainer == null)
        {
            _logger.LogWarning(
                "Trainer user not found for Invitation {InvitationId}, TrainerId {TrainerId}",
                command.InvitationId,
                invitation.TrainerId);
            return;
        }

        // Skip scheduling if recipient email is empty (graceful degradation)
        if (string.IsNullOrWhiteSpace(trainee.Email))
        {
            _logger.LogWarning(
                "Invitation email skipped for Invitation {InvitationId} - no recipient email provided",
                command.InvitationId);
            return;
        }

        // Map command to email payload with fetched data
        var emailPayload = new InvitationEmailPayload
        {
            InvitationId = command.InvitationId,
            InvitationCode = invitation.Code,
            ExpiresAt = invitation.ExpiresAt,
            TrainerName = trainer.Name,
            RecipientEmail = trainee.Email,
            CultureName = trainer.PreferredLanguage ?? "en-US"
        };

        // Schedule email via typed email scheduler
        await _emailScheduler.ScheduleAsync(emailPayload, cancellationToken);

        _logger.LogInformation(
            "Invitation email scheduled for Invitation {InvitationId} to {Email}",
            command.InvitationId,
            trainee.Email);
}
}