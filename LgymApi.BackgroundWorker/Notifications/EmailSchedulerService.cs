using System.Text.Json;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Notifications;

public sealed class EmailSchedulerService<TPayload> : IEmailScheduler<TPayload>
    where TPayload : IEmailPayload
{
    private const int MaxManualRequeueAttempts = 5;

    private readonly IEmailNotificationLogRepository _notificationLogRepository;
    private readonly IEmailBackgroundScheduler _backgroundScheduler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionalOutboxPublisher _outboxPublisher;
    private readonly IEmailNotificationsFeature _emailNotificationsFeature;
    private readonly IEmailMetrics _metrics;
    private readonly ILogger<EmailSchedulerService<TPayload>> _logger;

    public EmailSchedulerService(
        IEmailNotificationLogRepository notificationLogRepository,
        IEmailBackgroundScheduler backgroundScheduler,
        IUnitOfWork unitOfWork,
        ITransactionalOutboxPublisher outboxPublisher,
        IEmailNotificationsFeature emailNotificationsFeature,
        IEmailMetrics metrics,
        ILogger<EmailSchedulerService<TPayload>> logger)
    {
        _notificationLogRepository = notificationLogRepository;
        _backgroundScheduler = backgroundScheduler;
        _unitOfWork = unitOfWork;
        _outboxPublisher = outboxPublisher;
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

        var message = new NotificationMessage
        {
            Id = Guid.NewGuid(),
            Channel = NotificationChannel.Email,
            Type = payload.NotificationType,
            CorrelationId = payload.CorrelationId,
            Recipient = payload.RecipientEmail,
            PayloadJson = JsonSerializer.Serialize(payload)
        };

        if (!await TryPersistNotificationAsync(payload, message, cancellationToken))
        {
            return;
        }

        _metrics.RecordEnqueued(payload.NotificationType);
        _logger.LogInformation(
            "Created email notification {NotificationId} and persisted outbox event for {NotificationType} correlation {CorrelationId}.",
            message.Id,
            payload.NotificationType,
            payload.CorrelationId);
    }

    private bool IsSchedulingEnabled(EmailNotificationType notificationType, Guid correlationId)
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

    private void HandleExistingNotification(TPayload payload, NotificationMessage existing)
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

        _metrics.RecordRetried(payload.NotificationType);
        _logger.LogInformation(
            "Skipped duplicate enqueue for failed email notification {NotificationId}; outbox dispatch will handle retries (attempts: {Attempts}).",
            existing.Id,
            existing.Attempts);
    }

    private async Task<bool> TryPersistNotificationAsync(
        TPayload payload,
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationLogRepository.AddAsync(message, cancellationToken);
            await _outboxPublisher.PublishAsync(
                new OutboxEventEnvelope(
                    OutboxEventTypes.EmailNotificationScheduled,
                    JsonSerializer.Serialize(new EmailNotificationScheduledEvent(
                        message.Id,
                        message.CorrelationId,
                        message.Recipient,
                        message.Type.Value)),
                    message.CorrelationId),
                cancellationToken);
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
            _metrics.RecordEnqueued(payload.NotificationType);
            return false;
        }
    }
}
