using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common.Outbox;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Outbox;

public sealed class OutboxDispatcherService : IOutboxDispatcher
{
    private const int DefaultBatchSize = 50;
    private static readonly TimeSpan DispatchFailureBaseDelay = TimeSpan.FromSeconds(30);

    private readonly IOutboxRepository _outboxRepository;
    private readonly IOutboxDeliveryBackgroundScheduler _deliveryScheduler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReadOnlyList<IOutboxDeliveryHandler> _handlers;
    private readonly ILogger<OutboxDispatcherService> _logger;

    public OutboxDispatcherService(
        IOutboxRepository outboxRepository,
        IOutboxDeliveryBackgroundScheduler deliveryScheduler,
        IUnitOfWork unitOfWork,
        IEnumerable<IOutboxDeliveryHandler> handlers,
        ILogger<OutboxDispatcherService> logger)
    {
        _outboxRepository = outboxRepository;
        _deliveryScheduler = deliveryScheduler;
        _unitOfWork = unitOfWork;
        _handlers = handlers.ToList();
        _logger = logger;
    }

    public async Task DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var messages = await _outboxRepository.GetDispatchableMessagesAsync(DefaultBatchSize, now, cancellationToken);
        foreach (var candidate in messages)
        {
            if (!await _outboxRepository.TryMarkMessageProcessingAsync(candidate.Id, now, cancellationToken))
            {
                continue;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var message = await _outboxRepository.FindMessageByIdAsync(candidate.Id, cancellationToken);
            if (message == null)
            {
                continue;
            }

            try
            {
                var handlers = _handlers
                    .Where(x => x.EventDefinition.EventType == message.Type)
                    .ToList();
                foreach (var handler in handlers)
                {
                    var existing = await _outboxRepository.FindDeliveryAsync(message.Id, handler.HandlerName, cancellationToken);
                    if (existing != null)
                    {
                        continue;
                    }

                    var delivery = new OutboxDelivery
                    {
                        Id = Guid.NewGuid(),
                        EventId = message.Id,
                        HandlerName = handler.HandlerName,
                        Status = OutboxDeliveryStatus.Pending
                    };

                    await _outboxRepository.AddDeliveryAsync(delivery, cancellationToken);
                    _deliveryScheduler.Enqueue(delivery.Id);
                }

                message.Status = OutboxMessageStatus.Processed;
                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.LastError = null;
                message.NextAttemptAt = null;
            }
            catch (Exception exception)
            {
                message.Status = message.Attempts >= 5 ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;
                message.NextAttemptAt = message.Status == OutboxMessageStatus.Pending
                    ? DateTimeOffset.UtcNow.Add(ComputeBackoff(message.Attempts))
                    : null;
                message.LastError = ToSafeError(exception);
                _logger.LogError(exception, "Failed to dispatch outbox message {MessageId}", message.Id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var deliveries = await _outboxRepository.GetDispatchableDeliveriesAsync(DefaultBatchSize, now, cancellationToken);
        foreach (var delivery in deliveries)
        {
            _deliveryScheduler.Enqueue(delivery.Id);
        }
    }

    private static TimeSpan ComputeBackoff(int attempts)
    {
        var multiplier = Math.Min(Math.Max(attempts, 1), 8);
        return TimeSpan.FromSeconds(DispatchFailureBaseDelay.TotalSeconds * Math.Pow(2, multiplier - 1));
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
