using LgymApi.Application.Repositories;
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
    private readonly IUserRepository _userRepository;
    private readonly IEmailScheduler<WelcomeEmailPayload> _emailScheduler;
    private readonly ILogger<SendRegistrationEmailHandler> _logger;

    public SendRegistrationEmailHandler(
        IUserRepository userRepository,
        IEmailScheduler<WelcomeEmailPayload> emailScheduler,
        ILogger<SendRegistrationEmailHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _emailScheduler = emailScheduler ?? throw new ArgumentNullException(nameof(emailScheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(UserRegisteredCommand command, CancellationToken cancellationToken = default)
    {
        // Fetch user entity by ID
        var user = await _userRepository.FindByIdAsync(command.UserId, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning(
                "Welcome email skipped for User {UserId} - user not found",
                command.UserId);
            return;
        }

        // Skip scheduling if recipient email is empty (graceful degradation)
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning(
                "Welcome email skipped for User {UserId} - no recipient email provided",
                command.UserId);
            return;
        }

        // Determine culture: use user's preferred language or fallback to en-US
        var cultureName = !string.IsNullOrWhiteSpace(user.PreferredLanguage) ? user.PreferredLanguage : "en-US";

        // Map user entity to email payload
        var emailPayload = new WelcomeEmailPayload
        {
            UserId = command.UserId,
            UserName = user.Name,
            RecipientEmail = user.Email,
            CultureName = cultureName
        };

        // Schedule email via typed email scheduler
        await _emailScheduler.ScheduleAsync(emailPayload, cancellationToken);

        _logger.LogInformation(
            "Welcome email scheduled for User {UserId} to {Email}",
            command.UserId,
            user.Email);
    }
}
