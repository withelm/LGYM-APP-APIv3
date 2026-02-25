using System.Text.Json;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Notifications;

public sealed class WelcomeEmailSchedulerService : IWelcomeEmailScheduler
{
    public const string NotificationType = "user.registration.welcome";
    private const int MaxManualRequeueAttempts = 5;

    private readonly IEmailNotificationLogRepository _notificationLogRepository;
    private readonly IWelcomeEmailBackgroundScheduler _backgroundScheduler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailNotificationsFeature _emailNotificationsFeature;
    private readonly IWelcomeEmailMetrics _metrics;
    private readonly ILogger<WelcomeEmailSchedulerService> _logger;

    public WelcomeEmailSchedulerService(
        IEmailNotificationLogRepository notificationLogRepository,
        IWelcomeEmailBackgroundScheduler backgroundScheduler,
        IUnitOfWork unitOfWork,
        IEmailNotificationsFeature emailNotificationsFeature,
        IWelcomeEmailMetrics metrics,
        ILogger<WelcomeEmailSchedulerService> logger)
    {
        _notificationLogRepository = notificationLogRepository;
        _backgroundScheduler = backgroundScheduler;
        _unitOfWork = unitOfWork;
        _emailNotificationsFeature = emailNotificationsFeature;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task ScheduleWelcomeAsync(WelcomeEmailPayload payload, CancellationToken cancellationToken = default)
    {
        if (!IsSchedulingEnabled(payload.UserId))
        {
            return;
        }

        var existing = await _notificationLogRepository.FindByCorrelationAsync(
            NotificationType,
            payload.UserId,
            payload.RecipientEmail,
            cancellationToken);

        if (existing != null)
        {
            HandleExistingNotification(existing);
            return;
        }

        var log = new EmailNotificationLog
        {
            Id = Guid.NewGuid(),
            Type = NotificationType,
            CorrelationId = payload.UserId,
            RecipientEmail = payload.RecipientEmail,
            PayloadJson = JsonSerializer.Serialize(payload)
        };

        if (!await TryPersistNotificationAsync(payload, log, cancellationToken))
        {
            return;
        }

        _backgroundScheduler.Enqueue(log.Id);
        _metrics.RecordEnqueued();
        _logger.LogInformation(
            "Created and enqueued welcome email notification {NotificationId} for user {UserId}.",
            log.Id,
            payload.UserId);
    }

    private bool IsSchedulingEnabled(Guid userId)
    {
        if (_emailNotificationsFeature.Enabled)
        {
            return true;
        }

        _logger.LogInformation(
            "Email notifications are disabled; skipping welcome email scheduling for user {UserId}.",
            userId);
        return false;
    }

    private void HandleExistingNotification(EmailNotificationLog existing)
    {
        if (existing.Status != EmailNotificationStatus.Failed)
        {
            _logger.LogInformation(
                "Found existing welcome email notification {NotificationId} with status {Status}; no new notification created.",
                existing.Id,
                existing.Status);
            return;
        }

        if (existing.Attempts >= MaxManualRequeueAttempts)
        {
            _logger.LogWarning(
                "Skipping re-enqueue for notification {NotificationId} because attempts reached limit {MaxAttempts}.",
                existing.Id,
                MaxManualRequeueAttempts);
            return;
        }

        _backgroundScheduler.Enqueue(existing.Id);
        _metrics.RecordRetried();
        _logger.LogInformation(
            "Re-enqueued failed welcome email notification {NotificationId} (attempts: {Attempts}).",
            existing.Id,
            existing.Attempts);
    }

    private async Task<bool> TryPersistNotificationAsync(
        WelcomeEmailPayload payload,
        EmailNotificationLog log,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationLogRepository.AddAsync(log, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            var concurrent = await _notificationLogRepository.FindByCorrelationAsync(
                NotificationType,
                payload.UserId,
                payload.RecipientEmail,
                cancellationToken);

            if (concurrent == null)
            {
                throw;
            }

            _logger.LogWarning(
                ex,
                "Detected concurrent welcome email scheduling for user {UserId}; using existing notification {NotificationId}.",
                payload.UserId,
                concurrent.Id);
            _backgroundScheduler.Enqueue(concurrent.Id);
            _metrics.RecordEnqueued();
            return false;
        }
    }
}
