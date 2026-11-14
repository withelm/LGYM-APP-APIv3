using System.Diagnostics;
using LgymApi.Application.Repositories;
using LgymApi.Identity.Contracts.AccountDeletion;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Notifications;

internal sealed class InAppNotificationRetentionCleanupService : IInAppNotificationRetentionCleanupService
{
    private readonly IInAppNotificationRepository _inAppNotificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationRetentionSettings _settings;
    private readonly ILogger<InAppNotificationRetentionCleanupService> _logger;
    private readonly IAccountDeletionRetention _accountRetention;

    public InAppNotificationRetentionCleanupService(
        IInAppNotificationRepository inAppNotificationRepository,
        IUnitOfWork unitOfWork,
        INotificationRetentionSettings settings,
        ILogger<InAppNotificationRetentionCleanupService> logger,
        IAccountDeletionRetention accountRetention)
    {
        _inAppNotificationRepository = inAppNotificationRepository;
        _unitOfWork = unitOfWork;
        _settings = settings;
        _logger = logger;
        _accountRetention = accountRetention;
    }

    public async Task<int> CleanupAsync(CancellationToken cancellationToken = default)
    {
        const string operation = "in-app-notification-retention-cleanup";
        var cutoff = DateTimeOffset.UtcNow.AddDays(-_settings.InAppNotificationDays);
        var stopwatch = Stopwatch.StartNew();
        var removedCount = 0;
        var batchCount = 0;

        _logger.LogInformation(
            "Retention cleanup {Operation} started before cutoff {CutoffUtc} with batch size {BatchSize}.",
            operation,
            cutoff,
            _settings.BatchSize);

        try
        {
            while (true)
            {
                var candidates = await _inAppNotificationRepository.GetRetentionCandidatesCreatedBeforeAsync(
                    cutoff,
                    _settings.BatchSize,
                    await _accountRetention.GetProtectedAccountIdsAsync(cancellationToken),
                    cancellationToken);
                if (candidates.Count == 0)
                {
                    break;
                }

                await _accountRetention.EnsureEligibleAsync(candidates
                    .SelectMany(item => item.SenderUserId.HasValue ? new[] { item.RecipientId, item.SenderUserId.Value } : new[] { item.RecipientId })
                    .Select(id => id.Rebind<LgymApi.Identity.Contracts.AccountReference>()).Distinct().ToArray(), cancellationToken);
                _inAppNotificationRepository.RemoveRange(candidates);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                removedCount += candidates.Count;
                batchCount++;
            }

            _logger.LogInformation(
                "Retention cleanup {Operation} completed with {DeletedCount} rows in {BatchCount} batches before cutoff {CutoffUtc} in {Duration}.",
                operation,
                removedCount,
                batchCount,
                cutoff,
                stopwatch.Elapsed);
            return removedCount;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Retention cleanup {Operation} failed after deleting {DeletedCount} rows in {BatchCount} batches before cutoff {CutoffUtc} in {Duration}.",
                operation,
                removedCount,
                batchCount,
                cutoff,
                stopwatch.Elapsed);
            throw;
        }
    }
}
