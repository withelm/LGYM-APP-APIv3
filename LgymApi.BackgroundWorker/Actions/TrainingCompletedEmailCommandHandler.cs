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
    private readonly IUserRepository _userRepository;
    private readonly ITrainingRepository _trainingRepository;
    private readonly ITrainingExerciseScoreRepository _trainingExerciseScoreRepository;
    private readonly IExerciseScoreRepository _exerciseScoreRepository;
    private readonly IEmailNotificationSubscriptionRepository _emailNotificationSubscriptionRepository;
    private readonly IEmailScheduler<TrainingCompletedEmailPayload> _emailScheduler;
    private readonly ILogger<TrainingCompletedEmailCommandHandler> _logger;

    public TrainingCompletedEmailCommandHandler(
        IUserRepository userRepository,
        ITrainingRepository trainingRepository,
        ITrainingExerciseScoreRepository trainingExerciseScoreRepository,
        IExerciseScoreRepository exerciseScoreRepository,
        IEmailNotificationSubscriptionRepository emailNotificationSubscriptionRepository,
        IEmailScheduler<TrainingCompletedEmailPayload> emailScheduler,
        ILogger<TrainingCompletedEmailCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _trainingRepository = trainingRepository ?? throw new ArgumentNullException(nameof(trainingRepository));
        _trainingExerciseScoreRepository = trainingExerciseScoreRepository ?? throw new ArgumentNullException(nameof(trainingExerciseScoreRepository));
        _exerciseScoreRepository = exerciseScoreRepository ?? throw new ArgumentNullException(nameof(exerciseScoreRepository));
        _emailNotificationSubscriptionRepository = emailNotificationSubscriptionRepository ?? throw new ArgumentNullException(nameof(emailNotificationSubscriptionRepository));
        _emailScheduler = emailScheduler ?? throw new ArgumentNullException(nameof(emailScheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(TrainingCompletedCommand command, CancellationToken cancellationToken = default)
    {
        // Fetch user by ID
        var user = await _userRepository.FindByIdAsync(command.UserId, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning(
                "Training completed email skipped for Training {TrainingId} - user {UserId} not found",
                command.TrainingId,
                command.UserId);
            return;
        }

        // Skip scheduling if recipient email is empty (graceful degradation)
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning(
                "Training completed email skipped for Training {TrainingId} - no recipient email for user {UserId}",
                command.TrainingId,
                command.UserId);
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

        // Fetch training exercises
        var trainingExercises = await _trainingExerciseScoreRepository.GetByTrainingIdsAsync(
            new List<Guid> { command.TrainingId },
            cancellationToken);

        var exerciseScoreIds = trainingExercises.Select(te => te.ExerciseScoreId).ToList();
        var exerciseScores = exerciseScoreIds.Any()
            ? await _exerciseScoreRepository.GetByIdsAsync(exerciseScoreIds, cancellationToken)
            : new List<Domain.Entities.ExerciseScore>();

        // Build exercise summaries
        var exercises = trainingExercises
            .Select(te =>
            {
                var score = exerciseScores.FirstOrDefault(es => es.Id == te.ExerciseScoreId);
                return new TrainingExerciseSummary
                {
                    ExerciseId = score?.ExerciseId.ToString() ?? string.Empty,
                    ExerciseName = score?.Exercise?.Name ?? string.Empty,
                    Series = score?.Series ?? 0,
                    Reps = score?.Reps ?? 0,
                    Weight = score?.Weight ?? 0,
                    Unit = score?.Unit ?? Domain.Enums.WeightUnits.Kilograms
                };
            })
            .ToList();

        // Fetch training to get plan day name and training date
        var training = await _trainingRepository.GetByIdAsync(command.TrainingId, cancellationToken);
        if (training == null)
        {
            _logger.LogWarning(
                "Training completed email skipped for Training {TrainingId} - training not found",
                command.TrainingId);
            return;
        }

        var planDayName = training.PlanDay?.Name ?? string.Empty;
        var trainingDate = training.CreatedAt;

        // Map command to email payload
        var emailPayload = new TrainingCompletedEmailPayload
        {
            UserId = command.UserId,
            TrainingId = command.TrainingId,
            RecipientEmail = user.Email,
            CultureName = user.PreferredLanguage ?? "en-US",
            TimeZoneId = user.PreferredTimeZone,
            PlanDayName = planDayName,
            TrainingDate = trainingDate,
            Exercises = exercises
        };

        // Schedule email via typed email scheduler
        await _emailScheduler.ScheduleAsync(emailPayload, cancellationToken);

        _logger.LogInformation(
            "Training completed email scheduled for Training {TrainingId} to {Email}",
            command.TrainingId,
            user.Email);
}

}
