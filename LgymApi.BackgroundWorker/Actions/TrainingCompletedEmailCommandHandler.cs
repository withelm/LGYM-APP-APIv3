using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class TrainingCompletedEmailCommandHandler : IBackgroundAction<TrainingCompletedCommand>
{
    private readonly IEmailScheduler<TrainingCompletedEmailPayload> _emailScheduler;
    private readonly ILogger<TrainingCompletedEmailCommandHandler> _logger;

    public TrainingCompletedEmailCommandHandler(
        IEmailScheduler<TrainingCompletedEmailPayload> emailScheduler,
        ILogger<TrainingCompletedEmailCommandHandler> logger)
    {
        _emailScheduler = emailScheduler;
        _logger = logger;
    }

    public async Task ExecuteAsync(TrainingCompletedCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.RecipientEmail))
        {
            _logger.LogInformation(
                "Training {TrainingId} has no recipient email. Skipping training completed email.",
                command.TrainingId);
            return;
        }

        var payload = new TrainingCompletedEmailPayload
        {
            UserId = command.UserId,
            TrainingId = command.TrainingId,
            RecipientEmail = command.RecipientEmail,
            CultureName = string.IsNullOrWhiteSpace(command.CultureName) ? "en-US" : command.CultureName,
            PlanDayName = command.PlanDayName ?? string.Empty,
            TrainingDate = new DateTimeOffset(command.CreatedAtUtc),
            Exercises = command.ExerciseDetails
                .Select(x => new TrainingExerciseSummary
                {
                    ExerciseName = x.ExerciseName,
                    Series = x.Series,
                    Reps = x.Reps,
                    Weight = x.Weight,
                    Unit = x.Unit
                })
                .ToList()
        };

        await _emailScheduler.ScheduleAsync(payload, cancellationToken);

        _logger.LogInformation(
            "Scheduled training completed email for training {TrainingId} to {Email}.",
            command.TrainingId,
            command.RecipientEmail);
    }
}
