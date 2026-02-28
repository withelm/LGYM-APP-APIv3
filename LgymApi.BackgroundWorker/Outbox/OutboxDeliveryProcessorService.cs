using LgymApi.BackgroundWorker.Common.Outbox;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker.Outbox;

public sealed class OutboxDeliveryProcessorService : IOutboxDeliveryProcessor
{
    private static readonly TimeSpan DeliveryFailureBaseDelay = TimeSpan.FromSeconds(30);

    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReadOnlyList<IOutboxDeliveryHandler> _handlers;
    private readonly ILogger<OutboxDeliveryProcessorService> _logger;

    public OutboxDeliveryProcessorService(
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        IEnumerable<IOutboxDeliveryHandler> handlers,
        ILogger<OutboxDeliveryProcessorService> logger)
    {
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _handlers = handlers.ToList();
        _logger = logger;
    }

    public async Task ProcessAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (!await _outboxRepository.TryMarkDeliveryProcessingAsync(deliveryId, now, cancellationToken))
        {
            return;
        }

        var delivery = await _outboxRepository.FindDeliveryByIdWithEventAsync(deliveryId, cancellationToken);
        if (delivery == null || delivery.Event == null)
        {
            return;
        }

        if (delivery.Status == OutboxDeliveryStatus.Succeeded)
        {
            return;
        }

        try
        {
            var handler = _handlers.FirstOrDefault(x => string.Equals(x.HandlerName, delivery.HandlerName, StringComparison.Ordinal));
            if (handler == null)
            {
                throw new InvalidOperationException($"Outbox handler '{delivery.HandlerName}' is not registered.");
            }

            await handler.HandleAsync(
                delivery.EventId,
                delivery.Event.CorrelationId,
                delivery.Event.PayloadJson,
                cancellationToken);

            delivery.Status = OutboxDeliveryStatus.Succeeded;
            delivery.ProcessedAt = DateTimeOffset.UtcNow;
            delivery.NextAttemptAt = null;
            delivery.LastError = null;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            var shouldRetry = delivery.Attempts < 5;
            delivery.Status = shouldRetry ? OutboxDeliveryStatus.Pending : OutboxDeliveryStatus.Failed;
            delivery.NextAttemptAt = shouldRetry ? DateTimeOffset.UtcNow.Add(ComputeBackoff(delivery.Attempts)) : null;
            delivery.LastError = ToSafeError(exception);
            delivery.ProcessedAt = shouldRetry ? null : DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogError(exception, "Failed processing outbox delivery {DeliveryId}", deliveryId);
            if (!shouldRetry)
            {
                throw;
            }
        }
    }

    private static TimeSpan ComputeBackoff(int attempts)
    {
        var multiplier = Math.Min(Math.Max(attempts, 1), 8);
        return TimeSpan.FromSeconds(DeliveryFailureBaseDelay.TotalSeconds * Math.Pow(2, multiplier - 1));
    }

    private static string ToSafeError(Exception exception)
    {
        var message = exception.GetType().Name;
        if (!string.IsNullOrWhiteSpace(exception.Message))
        {
            message = $"{message}: {exception.Message}";
        }

        message = message.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        return message.Length <= 400 ? message : message[..400];
    }
}
