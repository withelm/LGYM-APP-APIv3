using System.Text.Json;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Notifications;

public sealed class WelcomeEmailJobHandlerService : IWelcomeEmailJobHandler
{
    private readonly IEmailNotificationLogRepository _notificationLogRepository;
    private readonly IEmailTemplateComposer _templateComposer;
    private readonly IEmailSender _emailSender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWelcomeEmailMetrics _metrics;
    private readonly ILogger<WelcomeEmailJobHandlerService> _logger;

    public WelcomeEmailJobHandlerService(
        IEmailNotificationLogRepository notificationLogRepository,
        IEmailTemplateComposer templateComposer,
        IEmailSender emailSender,
        IUnitOfWork unitOfWork,
        IWelcomeEmailMetrics metrics,
        ILogger<WelcomeEmailJobHandlerService> logger)
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
                "Welcome email notification {NotificationId} was not found. The job will be skipped.",
                notificationId);
            return;
        }

        if (notification.Status == EmailNotificationStatus.Sent)
        {
            _logger.LogInformation(
                "Welcome email notification {NotificationId} is already sent; skipping duplicate processing.",
                notificationId);
            return;
        }

        notification.Attempts += 1;
        notification.LastAttemptAt = DateTimeOffset.UtcNow;

        if (notification.Attempts > 1)
        {
            _metrics.RecordRetried();
            _logger.LogInformation(
                "Retrying welcome email notification {NotificationId} (attempt {Attempt}).",
                notificationId,
                notification.Attempts);
        }

        WelcomeEmailPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WelcomeEmailPayload>(notification.PayloadJson);
        }
        catch (Exception ex)
        {
            notification.Status = EmailNotificationStatus.Failed;
            notification.LastError = ToSafeError(ex);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _metrics.RecordFailed();
            _logger.LogError(ex, "Failed to deserialize welcome email payload for notification {NotificationId}.", notificationId);
            throw new InvalidOperationException($"Failed to deserialize welcome email payload for notification {notificationId}.", ex);
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
            message = _templateComposer.ComposeWelcome(payload);
        }
        catch (Exception ex)
        {
            notification.Status = EmailNotificationStatus.Failed;
            notification.LastError = ToSafeError(ex);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _metrics.RecordFailed();
            _logger.LogError(ex, "Failed to compose welcome email template for notification {NotificationId}.", notificationId);
            throw new InvalidOperationException($"Failed to compose welcome email template for notification {notificationId}.", ex);
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
                "Welcome email notification {NotificationId} sent successfully on attempt {Attempt}.",
                notificationId,
                notification.Attempts);
        }
        catch (Exception ex)
        {
            notification.Status = EmailNotificationStatus.Failed;
            notification.LastError = ToSafeError(ex);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _metrics.RecordFailed();
            _logger.LogError(ex, "Failed to send welcome email for notification {NotificationId}.", notificationId);
            throw new InvalidOperationException($"Failed to send welcome email for notification {notificationId}.", ex);
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
