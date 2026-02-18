using System.Text.Json;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Notifications;

public sealed class InvitationEmailJobHandlerService : IInvitationEmailJobHandler
{
    private readonly IEmailNotificationLogRepository _notificationLogRepository;
    private readonly IEmailTemplateComposer _templateComposer;
    private readonly IEmailSender _emailSender;
    private readonly IUnitOfWork _unitOfWork;

    public InvitationEmailJobHandlerService(
        IEmailNotificationLogRepository notificationLogRepository,
        IEmailTemplateComposer templateComposer,
        IEmailSender emailSender,
        IUnitOfWork unitOfWork)
    {
        _notificationLogRepository = notificationLogRepository;
        _templateComposer = templateComposer;
        _emailSender = emailSender;
        _unitOfWork = unitOfWork;
    }

    public async Task ProcessAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _notificationLogRepository.FindByIdAsync(notificationId, cancellationToken);
        if (notification == null)
        {
            return;
        }

        if (notification.Status == EmailNotificationStatus.Sent)
        {
            return;
        }

        notification.Attempts += 1;
        notification.LastAttemptAt = DateTimeOffset.UtcNow;

        InvitationEmailPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<InvitationEmailPayload>(notification.PayloadJson);
        }
        catch (Exception ex)
        {
            notification.Status = EmailNotificationStatus.Failed;
            notification.LastError = ex.Message;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }

        if (payload == null)
        {
            notification.Status = EmailNotificationStatus.Failed;
            notification.LastError = "Email payload is empty.";
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var message = _templateComposer.ComposeTrainerInvitation(payload);

        try
        {
            await _emailSender.SendAsync(message, cancellationToken);
            notification.Status = EmailNotificationStatus.Sent;
            notification.SentAt = DateTimeOffset.UtcNow;
            notification.LastError = null;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            notification.Status = EmailNotificationStatus.Failed;
            notification.LastError = ex.Message;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
