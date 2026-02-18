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
    private readonly ILogger<InvitationEmailSchedulerService> _logger;

    public InvitationEmailSchedulerService(
        IEmailNotificationLogRepository notificationLogRepository,
        IInvitationEmailBackgroundScheduler backgroundScheduler,
        IUnitOfWork unitOfWork,
        ILogger<InvitationEmailSchedulerService> logger)
    {
        _notificationLogRepository = notificationLogRepository;
        _backgroundScheduler = backgroundScheduler;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ScheduleInvitationCreatedAsync(InvitationEmailPayload payload, CancellationToken cancellationToken = default)
    {
        var existing = await _notificationLogRepository.FindByCorrelationAsync(
            NotificationType,
            payload.InvitationId,
            payload.RecipientEmail,
            cancellationToken);

        if (existing != null)
        {
            if (existing.Status == EmailNotificationStatus.Failed)
            {
                if (existing.Attempts < MaxManualRequeueAttempts)
                {
                    _backgroundScheduler.Enqueue(existing.Id);
                    _logger.LogInformation(
                        "Re-enqueued failed invitation email notification {NotificationId} (attempts: {Attempts}).",
                        existing.Id,
                        existing.Attempts);
                }
                else
                {
                    _logger.LogWarning(
                        "Skipping re-enqueue for notification {NotificationId} because attempts reached limit {MaxAttempts}.",
                        existing.Id,
                        MaxManualRequeueAttempts);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Found existing invitation email notification {NotificationId} with status {Status}; no new notification created.",
                    existing.Id,
                    existing.Status);
            }

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

        await _notificationLogRepository.AddAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _backgroundScheduler.Enqueue(log.Id);
        _logger.LogInformation(
            "Created and enqueued invitation email notification {NotificationId} for invitation {InvitationId}.",
            log.Id,
            payload.InvitationId);
    }
}
