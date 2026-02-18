using System.Text.Json;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Notifications;

public sealed class InvitationEmailSchedulerService : IInvitationEmailScheduler
{
    public const string NotificationType = "trainer.invitation.created";

    private readonly IEmailNotificationLogRepository _notificationLogRepository;
    private readonly IInvitationEmailBackgroundScheduler _backgroundScheduler;
    private readonly IUnitOfWork _unitOfWork;

    public InvitationEmailSchedulerService(
        IEmailNotificationLogRepository notificationLogRepository,
        IInvitationEmailBackgroundScheduler backgroundScheduler,
        IUnitOfWork unitOfWork)
    {
        _notificationLogRepository = notificationLogRepository;
        _backgroundScheduler = backgroundScheduler;
        _unitOfWork = unitOfWork;
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
                _backgroundScheduler.Enqueue(existing.Id);
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
    }
}
