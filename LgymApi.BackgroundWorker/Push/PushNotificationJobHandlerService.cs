using System.Text.Json;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Application.Notifications.Repositories;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Notifications.Push;
using Microsoft.Extensions.Logging;
using LgymApi.Infrastructure.Options;

namespace LgymApi.BackgroundWorker.Push;

public sealed class PushNotificationJobHandlerService
{
    private const string InvalidTokenDisabledReason = "InvalidFcmToken";

    private readonly IPushNotificationMessageRepository _pushNotificationMessageRepository;
    private readonly IPushInstallationRepository _pushInstallationRepository;
    private readonly IPushProviderSender _pushProviderSender;
    private readonly IPushBackgroundScheduler _pushBackgroundScheduler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PushNotificationOptions _options;
    private readonly ILogger<PushNotificationJobHandlerService> _logger;

    public PushNotificationJobHandlerService(
        IPushNotificationMessageRepository pushNotificationMessageRepository,
        IPushInstallationRepository pushInstallationRepository,
        IPushProviderSender pushProviderSender,
        IPushBackgroundScheduler pushBackgroundScheduler,
        IUnitOfWork unitOfWork,
        PushNotificationOptions options,
        ILogger<PushNotificationJobHandlerService> logger)
    {
        _pushNotificationMessageRepository = pushNotificationMessageRepository;
        _pushInstallationRepository = pushInstallationRepository;
        _pushProviderSender = pushProviderSender;
        _pushBackgroundScheduler = pushBackgroundScheduler;
        _unitOfWork = unitOfWork;
        _options = options;
        _logger = logger;
    }

    public async Task ProcessAsync(Id<PushNotificationMessage> notificationId, CancellationToken cancellationToken = default)
    {
        var claimed = await _pushNotificationMessageRepository.TryTransitionToSendingAsync(notificationId, cancellationToken);
        if (!claimed)
        {
            _logger.LogInformation(
                "Push notification {NotificationId} could not be claimed for sending; skipping duplicate work.",
                notificationId);
            return;
        }

        var message = await _pushNotificationMessageRepository.FindByIdAsync(notificationId, cancellationToken);
        if (message == null)
        {
            _logger.LogWarning("Push notification {NotificationId} was not found after claim.", notificationId);
            return;
        }

        var installation = await _pushInstallationRepository.FindByIdAsync(message.PushInstallationId, cancellationToken);
        if (installation == null)
        {
            message.Status = PushNotificationStatus.Failed;
            message.FailureKind = PushNotificationFailureKind.Permanent;
            message.ProviderStatus = "InstallationMissing";
            message.LastError = "Push installation no longer exists.";
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        message.Attempts += 1;
        var payload = JsonSerializer.Deserialize<PushEventPayload>(message.PayloadJson, SharedSerializationOptions.Current)
            ?? throw new InvalidOperationException($"Failed to deserialize push payload for notification {notificationId}.");

        var target = new PushDeliveryTarget(installation.InstallationId, installation.FcmToken);
        var result = await _pushProviderSender.SendAsync(target, payload, cancellationToken);
        ApplyProviderResult(message, installation, result);
        _logger.LogInformation(
            "Processed push notification {NotificationId} for installation {InstallationId}, event {EventId}, category {Category} with provider status {ProviderStatus} and outcome {Outcome}.",
            message.Id,
            installation.InstallationId,
            message.EventId,
            message.Type,
            result.ProviderStatus,
            result.Outcome);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (result.Outcome == PushSendOutcome.TransientFailure)
        {
            ScheduleRetryIfAvailable(message);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private void ApplyProviderResult(PushNotificationMessage message, PushInstallation installation, PushSendAttemptResult result)
    {
        message.ProviderStatus = result.ProviderStatus;
        message.ProviderMessageId = result.ProviderMessageId;
        message.ProviderErrorCode = result.ProviderErrorCode;
        message.ProviderResponseSummary = result.ProviderResponseSummary;
        message.LastError = result.ProviderResponseSummary;

        switch (result.Outcome)
        {
            case PushSendOutcome.Sent:
                message.Status = PushNotificationStatus.Sent;
                message.FailureKind = PushNotificationFailureKind.None;
                message.SentAt = DateTimeOffset.UtcNow;
                message.NextAttemptAt = null;
                message.LastError = null;
                break;

            case PushSendOutcome.Skipped:
                message.Status = PushNotificationStatus.Skipped;
                message.FailureKind = PushNotificationFailureKind.Preference;
                message.NextAttemptAt = null;
                break;

            case PushSendOutcome.InvalidToken:
                message.Status = PushNotificationStatus.Failed;
                message.FailureKind = PushNotificationFailureKind.InvalidToken;
                message.NextAttemptAt = null;
                installation.DisabledAt = DateTimeOffset.UtcNow;
                installation.DisabledReason = InvalidTokenDisabledReason;
                installation.LastSeenAt = DateTimeOffset.UtcNow;
                break;

            case PushSendOutcome.TransientFailure:
                message.Status = PushNotificationStatus.Failed;
                message.FailureKind = PushNotificationFailureKind.Transient;
                message.NextAttemptAt = ResolveNextAttemptAt(message.Attempts);
                break;

            default:
                message.Status = PushNotificationStatus.Failed;
                message.FailureKind = PushNotificationFailureKind.Permanent;
                message.NextAttemptAt = null;
                break;
        }
    }

    private void ScheduleRetryIfAvailable(PushNotificationMessage message)
    {
        var delay = ResolveRetryDelay(message.Attempts);
        if (delay == null)
        {
            _logger.LogWarning(
                "Transient push notification {NotificationId} exhausted retry attempts at attempt {Attempt}.",
                message.Id,
                message.Attempts);
            message.NextAttemptAt = null;
            return;
        }

        message.SchedulerJobId = _pushBackgroundScheduler.ScheduleRetry(message.Id, delay.Value);
    }

    private DateTimeOffset? ResolveNextAttemptAt(int attempts)
    {
        var delay = ResolveRetryDelay(attempts);
        return delay == null ? null : DateTimeOffset.UtcNow.Add(delay.Value);
    }

    private TimeSpan? ResolveRetryDelay(int attempts)
    {
        if (attempts <= 0 || attempts > _options.RetryDelaysSeconds.Length)
        {
            return null;
        }

        return TimeSpan.FromSeconds(_options.RetryDelaysSeconds[attempts - 1]);
    }
}
