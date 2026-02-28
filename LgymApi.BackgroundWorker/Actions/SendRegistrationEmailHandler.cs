using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

/// <summary>
/// Background action handler that schedules welcome email notifications after user registration.
/// Triggered when a new user successfully completes registration.
/// </summary>
public sealed class SendRegistrationEmailHandler : IBackgroundAction<UserRegisteredCommand>
{
    private readonly IEmailScheduler<WelcomeEmailPayload> _emailScheduler;
    private readonly ILogger<SendRegistrationEmailHandler> _logger;

    public SendRegistrationEmailHandler(
        IEmailScheduler<WelcomeEmailPayload> emailScheduler,
        ILogger<SendRegistrationEmailHandler> logger)
    {
        _emailScheduler = emailScheduler ?? throw new ArgumentNullException(nameof(emailScheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(UserRegisteredCommand command, CancellationToken cancellationToken = default)
    {
        // Skip scheduling if recipient email is empty (graceful degradation)
        if (string.IsNullOrWhiteSpace(command.RecipientEmail))
        {
            _logger.LogWarning(
                "Welcome email skipped for User {UserId} - no recipient email provided",
                command.UserId);
            return;
        }

        // Map command to email payload
        var emailPayload = new WelcomeEmailPayload
        {
            UserId = command.UserId,
            UserName = command.UserName,
            RecipientEmail = command.RecipientEmail,
            CultureName = command.CultureName
        };

        // Schedule email via typed email scheduler
        await _emailScheduler.ScheduleAsync(emailPayload, cancellationToken);

        _logger.LogInformation(
            "Welcome email scheduled for User {UserId} to {Email}",
            command.UserId,
            command.RecipientEmail);
    }
}
