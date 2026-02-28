using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

/// <summary>
/// Background action handler that schedules training completed email notifications.
/// Triggered when a training session is completed.
/// </summary>
public sealed class TrainingCompletedEmailCommandHandler : IBackgroundAction<TrainingCompletedCommand>
{
    private readonly IEmailNotificationSubscriptionRepository _emailNotificationSubscriptionRepository;
    private readonly IEmailScheduler<TrainingCompletedEmailPayload> _emailScheduler;
    private readonly ILogger<TrainingCompletedEmailCommandHandler> _logger;

    public TrainingCompletedEmailCommandHandler(
        IEmailNotificationSubscriptionRepository emailNotificationSubscriptionRepository,
        IEmailScheduler<TrainingCompletedEmailPayload> emailScheduler,
        ILogger<TrainingCompletedEmailCommandHandler> logger)
    {
        _emailNotificationSubscriptionRepository = emailNotificationSubscriptionRepository ?? throw new ArgumentNullException(nameof(emailNotificationSubscriptionRepository));
        _emailScheduler = emailScheduler ?? throw new ArgumentNullException(nameof(emailScheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(TrainingCompletedCommand command, CancellationToken cancellationToken = default)
    {
        // Skip scheduling if recipient email is empty (graceful degradation)
        if (string.IsNullOrWhiteSpace(command.RecipientEmail))
        {
            _logger.LogWarning(
                "Training completed email skipped for Training {TrainingId} - no recipient email provided",
                command.TrainingId);
            return;
        }

        var isSubscribed = await _emailNotificationSubscriptionRepository.IsSubscribedAsync(
            command.UserId,
            EmailNotificationTypes.TrainingCompleted.Value,
            cancellationToken);

        if (!isSubscribed)
        {
            _logger.LogInformation(
                "Training completed email skipped for Training {TrainingId} - subscription is disabled for user {UserId}",
                command.TrainingId,
                command.UserId);
            return;
        }

        // Map command to email payload
        var emailPayload = new TrainingCompletedEmailPayload
        {
            UserId = command.UserId,
            TrainingId = command.TrainingId,
            RecipientEmail = command.RecipientEmail,
            CultureName = command.CultureName,
            PlanDayName = command.PlanDayName,
            TrainingDate = command.TrainingDate,
            Exercises = command.Exercises
        };

        // Schedule email via typed email scheduler
        await _emailScheduler.ScheduleAsync(emailPayload, cancellationToken);

        _logger.LogInformation(
            "Training completed email scheduled for Training {TrainingId} to {Email}",
            command.TrainingId,
            command.RecipientEmail);
    }
}
