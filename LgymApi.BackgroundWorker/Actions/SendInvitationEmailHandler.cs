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
    private readonly IEmailScheduler<InvitationEmailPayload> _emailScheduler;
    private readonly ILogger<SendInvitationEmailHandler> _logger;

    public SendInvitationEmailHandler(
        IEmailScheduler<InvitationEmailPayload> emailScheduler,
        ILogger<SendInvitationEmailHandler> logger)
    {
        _emailScheduler = emailScheduler ?? throw new ArgumentNullException(nameof(emailScheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(InvitationCreatedCommand command, CancellationToken cancellationToken = default)
    {
        // Skip scheduling if recipient email is empty (graceful degradation)
        if (string.IsNullOrWhiteSpace(command.RecipientEmail))
        {
            _logger.LogWarning(
                "Invitation email skipped for Invitation {InvitationId} - no recipient email provided",
                command.InvitationId);
            return;
        }

        // Map command to email payload
        var emailPayload = new InvitationEmailPayload
        {
            InvitationId = command.InvitationId,
            InvitationCode = command.InvitationCode,
            ExpiresAt = command.ExpiresAt,
            TrainerName = command.TrainerName,
            RecipientEmail = command.RecipientEmail,
            CultureName = command.CultureName
        };

        // Schedule email via typed email scheduler
        await _emailScheduler.ScheduleAsync(emailPayload, cancellationToken);

        _logger.LogInformation(
            "Invitation email scheduled for Invitation {InvitationId} to {Email}",
            command.InvitationId,
            command.RecipientEmail);
    }
}
