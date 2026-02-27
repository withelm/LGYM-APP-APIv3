using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Notifications;

public sealed class EmailJobHandlerService : IEmailJobHandler
{
    private readonly IEmailNotificationLogRepository _notificationLogRepository;
    private readonly IEmailTemplateComposerFactory _templateComposerFactory;
    private readonly IEmailSender _emailSender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailMetrics _metrics;
    private readonly ILogger<EmailJobHandlerService> _logger;

    public EmailJobHandlerService(
        IEmailNotificationLogRepository notificationLogRepository,
        IEmailTemplateComposerFactory templateComposerFactory,
        IEmailSender emailSender,
        IUnitOfWork unitOfWork,
        IEmailMetrics metrics,
        ILogger<EmailJobHandlerService> logger)
    {
        _notificationLogRepository = notificationLogRepository;
        _templateComposerFactory = templateComposerFactory;
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
                "Email notification {NotificationId} was not found. The job will be skipped.",
                notificationId);
            return;
        }

        if (notification.Status == EmailNotificationStatus.Sent)
        {
            _logger.LogInformation(
                "Email notification {NotificationId} is already sent; skipping duplicate processing.",
                notificationId);
            return;
        }

        notification.Attempts += 1;
        notification.LastAttemptAt = DateTimeOffset.UtcNow;

        if (notification.Attempts > 1)
        {
            _metrics.RecordRetried(notification.Type);
            _logger.LogInformation(
                "Retrying email notification {NotificationId} (attempt {Attempt}).",
                notificationId,
                notification.Attempts);
        }

        EmailMessage message;
        try
        {
            message = _templateComposerFactory.ComposeMessage(notification.Type, notification.PayloadJson);
        }
        catch (Exception ex)
        {
            notification.Status = EmailNotificationStatus.Failed;
            notification.LastError = ToSafeError(ex);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _metrics.RecordFailed(notification.Type);
            _logger.LogError(ex, "Failed to compose email template for notification {NotificationId}.", notificationId);
            throw new InvalidOperationException($"Failed to compose email template for notification {notificationId}.", ex);
        }

        try
        {
            var delivered = await _emailSender.SendAsync(message, cancellationToken);
            if (!delivered)
            {
                notification.Status = EmailNotificationStatus.Failed;
                notification.LastError = "Email sender is disabled.";
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _metrics.RecordFailed(notification.Type);
                _logger.LogWarning(
                    "Email sender is disabled; notification {NotificationId} was not delivered.",
                    notificationId);
                return;
            }

            notification.Status = EmailNotificationStatus.Sent;
            notification.SentAt = DateTimeOffset.UtcNow;
            notification.LastError = null;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _metrics.RecordSent(notification.Type);
            _logger.LogInformation(
                "Email notification {NotificationId} sent successfully on attempt {Attempt}.",
                notificationId,
                notification.Attempts);
        }
        catch (Exception ex)
        {
            notification.Status = EmailNotificationStatus.Failed;
            notification.LastError = ToSafeError(ex);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _metrics.RecordFailed(notification.Type);
            _logger.LogError(ex, "Failed to send email for notification {NotificationId}.", notificationId);
            throw new InvalidOperationException($"Failed to send email for notification {notificationId}.", ex);
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
