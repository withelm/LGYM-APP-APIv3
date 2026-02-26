using System.Text.Json;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Notifications;

public sealed class EmailSchedulerService<TPayload> : IEmailScheduler<TPayload>
    where TPayload : IEmailPayload
{
    private const int MaxManualRequeueAttempts = 5;

    private readonly IEmailNotificationLogRepository _notificationLogRepository;
    private readonly IEmailBackgroundScheduler _backgroundScheduler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailNotificationsFeature _emailNotificationsFeature;
    private readonly IEmailMetrics _metrics;
    private readonly ILogger<EmailSchedulerService<TPayload>> _logger;

    public EmailSchedulerService(
        IEmailNotificationLogRepository notificationLogRepository,
        IEmailBackgroundScheduler backgroundScheduler,
        IUnitOfWork unitOfWork,
        IEmailNotificationsFeature emailNotificationsFeature,
        IEmailMetrics metrics,
        ILogger<EmailSchedulerService<TPayload>> logger)
    {
        _notificationLogRepository = notificationLogRepository;
        _backgroundScheduler = backgroundScheduler;
        _unitOfWork = unitOfWork;
        _emailNotificationsFeature = emailNotificationsFeature;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task ScheduleAsync(TPayload payload, CancellationToken cancellationToken = default)
    {
        if (!IsSchedulingEnabled(payload.NotificationType, payload.CorrelationId))
        {
            return;
        }

        var existing = await _notificationLogRepository.FindByCorrelationAsync(
            payload.NotificationType,
            payload.CorrelationId,
            payload.RecipientEmail,
            cancellationToken);

        if (existing != null)
        {
            HandleExistingNotification(payload, existing);
            return;
        }

        var log = new EmailNotificationLog
        {
            Id = Guid.NewGuid(),
            Type = payload.NotificationType,
            CorrelationId = payload.CorrelationId,
            RecipientEmail = payload.RecipientEmail,
            PayloadJson = JsonSerializer.Serialize(payload)
        };

        if (!await TryPersistNotificationAsync(payload, log, cancellationToken))
        {
            return;
        }

        _backgroundScheduler.Enqueue(log.Id);
        _metrics.RecordEnqueued(payload.NotificationType);
        _logger.LogInformation(
            "Created and enqueued email notification {NotificationId} for {NotificationType} correlation {CorrelationId}.",
            log.Id,
            payload.NotificationType,
            payload.CorrelationId);
    }

    private bool IsSchedulingEnabled(string notificationType, Guid correlationId)
    {
        if (_emailNotificationsFeature.Enabled)
        {
            return true;
        }

        _logger.LogInformation(
            "Email notifications are disabled; skipping scheduling for {NotificationType} correlation {CorrelationId}.",
            notificationType,
            correlationId);
        return false;
    }

    private void HandleExistingNotification(TPayload payload, EmailNotificationLog existing)
    {
        if (existing.Status != EmailNotificationStatus.Failed)
        {
            _logger.LogInformation(
                "Found existing email notification {NotificationId} with status {Status}; no new notification created.",
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
        _metrics.RecordRetried(payload.NotificationType);
        _logger.LogInformation(
            "Re-enqueued failed email notification {NotificationId} (attempts: {Attempts}).",
            existing.Id,
            existing.Attempts);
    }

    private async Task<bool> TryPersistNotificationAsync(
        TPayload payload,
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
                payload.NotificationType,
                payload.CorrelationId,
                payload.RecipientEmail,
                cancellationToken);

            if (concurrent == null)
            {
                throw;
            }

            _logger.LogWarning(
                ex,
                "Detected concurrent email scheduling for {NotificationType} correlation {CorrelationId}; using existing notification {NotificationId}.",
                payload.NotificationType,
                payload.CorrelationId,
                concurrent.Id);
            _backgroundScheduler.Enqueue(concurrent.Id);
            _metrics.RecordEnqueued(payload.NotificationType);
            return false;
        }
    }
}
