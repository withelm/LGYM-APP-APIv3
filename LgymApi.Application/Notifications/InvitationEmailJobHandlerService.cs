using System.Text.Json;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Notifications;

public sealed class InvitationEmailJobHandlerService : IInvitationEmailJobHandler
{
    private readonly IEmailNotificationLogRepository _notificationLogRepository;
    private readonly IEmailTemplateComposer _templateComposer;
    private readonly IEmailSender _emailSender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InvitationEmailJobHandlerService> _logger;

    public InvitationEmailJobHandlerService(
        IEmailNotificationLogRepository notificationLogRepository,
        IEmailTemplateComposer templateComposer,
        IEmailSender emailSender,
        IUnitOfWork unitOfWork,
        ILogger<InvitationEmailJobHandlerService> logger)
    {
        _notificationLogRepository = notificationLogRepository;
        _templateComposer = templateComposer;
        _emailSender = emailSender;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _notificationLogRepository.FindByIdAsync(notificationId, cancellationToken);
        if (notification == null)
        {
            _logger.LogWarning(
                "Invitation email notification {NotificationId} was not found. The job will be skipped.",
                notificationId);
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
            notification.LastError = ToSafeError(ex);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, "Failed to deserialize invitation email payload for notification {NotificationId}.", notificationId);
            throw;
        }

        if (payload == null)
        {
            notification.Status = EmailNotificationStatus.Failed;
            notification.LastError = "Email payload is empty.";
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        EmailMessage message;
        try
        {
            message = _templateComposer.ComposeTrainerInvitation(payload);
        }
        catch (Exception ex)
        {
            notification.Status = EmailNotificationStatus.Failed;
            notification.LastError = ToSafeError(ex);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, "Failed to compose invitation email template for notification {NotificationId}.", notificationId);
            throw;
        }

        try
        {
            var delivered = await _emailSender.SendAsync(message, cancellationToken);
            if (!delivered)
            {
                notification.Status = EmailNotificationStatus.Failed;
                notification.LastError = "Email sender is disabled.";
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(
                    "Email sender is disabled; notification {NotificationId} was not delivered.",
                    notificationId);
                return;
            }

            notification.Status = EmailNotificationStatus.Sent;
            notification.SentAt = DateTimeOffset.UtcNow;
            notification.LastError = null;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            notification.Status = EmailNotificationStatus.Failed;
            notification.LastError = ToSafeError(ex);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, "Failed to send invitation email for notification {NotificationId}.", notificationId);
            throw;
        }
    }

    private static string ToSafeError(Exception exception)
    {
        var message = exception.GetType().Name;
        if (!string.IsNullOrWhiteSpace(exception.Message))
        {
            message = $"{message}: {exception.Message}";
        }

        message = message.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        return message.Length <= 400 ? message : message[..400];
    }
}
