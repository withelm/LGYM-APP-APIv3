using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

/// <summary>
/// Background action handler for UserRegisteredCommand.
/// Schedules a welcome email using existing email infrastructure.
/// </summary>
public sealed class UserRegisteredCommandHandler : IBackgroundAction<UserRegisteredCommand>
{
    private readonly IEmailScheduler<WelcomeEmailPayload> _emailScheduler;
    private readonly ILogger<UserRegisteredCommandHandler> _logger;

    public UserRegisteredCommandHandler(
        IEmailScheduler<WelcomeEmailPayload> emailScheduler,
        ILogger<UserRegisteredCommandHandler> logger)
    {
        _emailScheduler = emailScheduler;
        _logger = logger;
    }

    public async Task ExecuteAsync(UserRegisteredCommand command, CancellationToken cancellationToken = default)
    {
        // Guard: skip if email is missing (preserve existing no-op behavior)
        if (string.IsNullOrWhiteSpace(command.RecipientEmail))
        {
            _logger.LogInformation(
                "User {UserId} has no email address. Skipping welcome email.",
                command.UserId);
            return;
        }

        // Map command to payload
        var payload = new WelcomeEmailPayload
        {
            UserId = command.UserId,
            UserName = command.UserName,
            RecipientEmail = command.RecipientEmail,
            CultureName = string.IsNullOrWhiteSpace(command.CultureName) ? "en-US" : command.CultureName
        };

        await _emailScheduler.ScheduleAsync(payload, cancellationToken);

        _logger.LogInformation(
            "Scheduled welcome email for user {UserId} to {Email}.",
            command.UserId,
            command.RecipientEmail);
    }
}
