using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

/// <summary>
/// Background action handler for InvitationCreatedCommand.
/// Schedules an invitation email using existing email infrastructure.
/// </summary>
public sealed class InvitationCreatedCommandHandler : IBackgroundAction<InvitationCreatedCommand>
{
    private readonly IEmailScheduler<InvitationEmailPayload> _emailScheduler;
    private readonly ILogger<InvitationCreatedCommandHandler> _logger;

    public InvitationCreatedCommandHandler(
        IEmailScheduler<InvitationEmailPayload> emailScheduler,
        ILogger<InvitationCreatedCommandHandler> logger)
    {
        _emailScheduler = emailScheduler;
        _logger = logger;
    }

    public async Task ExecuteAsync(InvitationCreatedCommand command, CancellationToken cancellationToken = default)
    {
        // Guard: skip if email is missing (preserve existing no-op behavior)
        if (string.IsNullOrWhiteSpace(command.RecipientEmail))
        {
            _logger.LogInformation(
                "Invitation {InvitationId} has no recipient email. Skipping invitation email.",
                command.InvitationId);
            return;
        }

        // Map command to payload
        var payload = new InvitationEmailPayload
        {
            InvitationId = command.InvitationId,
            InvitationCode = command.InvitationCode,
            ExpiresAt = command.ExpiresAt,
            TrainerName = command.TrainerName,
            RecipientEmail = command.RecipientEmail,
            CultureName = string.IsNullOrWhiteSpace(command.CultureName) ? "en-US" : command.CultureName
        };

        await _emailScheduler.ScheduleAsync(payload, cancellationToken);

        _logger.LogInformation(
            "Scheduled invitation email for invitation {InvitationId} to {Email}.",
            command.InvitationId,
            command.RecipientEmail);
    }
}
