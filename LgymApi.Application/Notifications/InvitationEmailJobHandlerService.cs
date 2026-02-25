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
    private readonly IInvitationEmailMetrics _metrics;
    private readonly ILogger<InvitationEmailJobHandlerService> _logger;

    public InvitationEmailJobHandlerService(
        IEmailNotificationLogRepository notificationLogRepository,
        IEmailTemplateComposer templateComposer,
        IEmailSender emailSender,
        IUnitOfWork unitOfWork,
        IInvitationEmailMetrics metrics,
        ILogger<InvitationEmailJobHandlerService> logger)
    {
        _notificationLogRepository = notificationLogRepository;
        _templateComposer = templateComposer;
        _emailSender = emailSender;
        _unitOfWork = unitOfWork;
        _metrics = metrics;
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
            _logger.LogInformation(
                "Invitation email notification {NotificationId} is already sent; skipping duplicate processing.",
                notificationId);
            return;
        }

        notification.Attempts += 1;
        notification.LastAttemptAt = DateTimeOffset.UtcNow;

        if (notification.Attempts > 1)
        {
            _metrics.RecordRetried();
            _logger.LogInformation(
                "Retrying invitation email notification {NotificationId} (attempt {Attempt}).",
                notificationId,
                notification.Attempts);
        }

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
            _metrics.RecordFailed();
            _logger.LogError(ex, "Failed to deserialize invitation email payload for notification {NotificationId}.", notificationId);
            throw new InvalidOperationException($"Failed to deserialize invitation email payload for notification {notificationId}.", ex);
        }

        if (payload == null)
        {
            notification.Status = EmailNotificationStatus.Failed;
            notification.LastError = "Email payload is empty.";
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _metrics.RecordFailed();
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
            _metrics.RecordFailed();
            _logger.LogError(ex, "Failed to compose invitation email template for notification {NotificationId}.", notificationId);
            throw new InvalidOperationException($"Failed to compose invitation email template for notification {notificationId}.", ex);
        }

        try
        {
            var delivered = await _emailSender.SendAsync(message, cancellationToken);
            if (!delivered)
            {
                notification.Status = EmailNotificationStatus.Failed;
                notification.LastError = "Email sender is disabled.";
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _metrics.RecordFailed();
                _logger.LogWarning(
                    "Email sender is disabled; notification {NotificationId} was not delivered.",
                    notificationId);
                return;
            }

            notification.Status = EmailNotificationStatus.Sent;
            notification.SentAt = DateTimeOffset.UtcNow;
            notification.LastError = null;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _metrics.RecordSent();
            _logger.LogInformation(
                "Invitation email notification {NotificationId} sent successfully on attempt {Attempt}.",
                notificationId,
                notification.Attempts);
        }
        catch (Exception ex)
        {
            notification.Status = EmailNotificationStatus.Failed;
            notification.LastError = ToSafeError(ex);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _metrics.RecordFailed();
            _logger.LogError(ex, "Failed to send invitation email for notification {NotificationId}.", notificationId);
            throw new InvalidOperationException($"Failed to send invitation email for notification {notificationId}.", ex);
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
