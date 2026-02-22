using System.Text.Json;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Notifications;

public sealed class InvitationEmailSchedulerService : IInvitationEmailScheduler
{
    public const string NotificationType = "trainer.invitation.created";
    private const int MaxManualRequeueAttempts = 5;

    private readonly IEmailNotificationLogRepository _notificationLogRepository;
    private readonly IInvitationEmailBackgroundScheduler _backgroundScheduler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailNotificationsFeature _emailNotificationsFeature;
    private readonly ILogger<InvitationEmailSchedulerService> _logger;

    public InvitationEmailSchedulerService(
        IEmailNotificationLogRepository notificationLogRepository,
        IInvitationEmailBackgroundScheduler backgroundScheduler,
        IUnitOfWork unitOfWork,
        IEmailNotificationsFeature emailNotificationsFeature,
        ILogger<InvitationEmailSchedulerService> logger)
    {
        _notificationLogRepository = notificationLogRepository;
        _backgroundScheduler = backgroundScheduler;
        _unitOfWork = unitOfWork;
        _emailNotificationsFeature = emailNotificationsFeature;
        _logger = logger;
    }

    public async Task ScheduleInvitationCreatedAsync(InvitationEmailPayload payload, CancellationToken cancellationToken = default)
    {
        if (!IsSchedulingEnabled(payload.InvitationId))
        {
            return;
        }

        var existing = await _notificationLogRepository.FindByCorrelationAsync(
            NotificationType,
            payload.InvitationId,
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
            CorrelationId = payload.InvitationId,
            RecipientEmail = payload.RecipientEmail,
            PayloadJson = JsonSerializer.Serialize(payload)
        };

        if (!await TryPersistNotificationAsync(payload, log, cancellationToken))
        {
            return;
        }

        _backgroundScheduler.Enqueue(log.Id);
        LogInfo(
            "Created and enqueued invitation email notification {NotificationId} for invitation {InvitationId}.",
            log.Id,
            payload.InvitationId);
    }

    private bool IsSchedulingEnabled(Guid invitationId)
    {
        if (_emailNotificationsFeature.Enabled)
        {
            return true;
        }

        LogInfo(
            "Email notifications are disabled; skipping invitation email scheduling for invitation {InvitationId}.",
            invitationId);
        return false;
    }

    private void HandleExistingNotification(EmailNotificationLog existing)
    {
        if (existing.Status != EmailNotificationStatus.Failed)
        {
            LogInfo(
                "Found existing invitation email notification {NotificationId} with status {Status}; no new notification created.",
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
        LogInfo(
            "Re-enqueued failed invitation email notification {NotificationId} (attempts: {Attempts}).",
            existing.Id,
            existing.Attempts);
    }

    private async Task<bool> TryPersistNotificationAsync(
        InvitationEmailPayload payload,
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
                payload.InvitationId,
                payload.RecipientEmail,
                cancellationToken);

            if (concurrent == null)
            {
                throw;
            }

            _logger.LogWarning(
                ex,
                "Detected concurrent invitation email scheduling for invitation {InvitationId}; using existing notification {NotificationId}.",
                payload.InvitationId,
                concurrent.Id);
            _backgroundScheduler.Enqueue(concurrent.Id);
            return false;
        }
    }

    private void LogInfo(string message, params object?[] args)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(message, args);
        }
    }
}
