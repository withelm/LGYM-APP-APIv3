using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly AppDbContext _dbContext;

    public OutboxRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        return _dbContext.OutboxMessages.AddAsync(message, cancellationToken).AsTask();
    }

    public Task AddDeliveryAsync(OutboxDelivery delivery, CancellationToken cancellationToken = default)
    {
        return _dbContext.OutboxDeliveries.AddAsync(delivery, cancellationToken).AsTask();
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetDispatchableMessagesAsync(int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return await _dbContext.OutboxMessages
            .Where(x => !x.IsDeleted
                        && x.Status == OutboxMessageStatus.Pending
                        && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryMarkMessageProcessingAsync(Guid messageId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.OutboxMessages.FirstOrDefaultAsync(
            x => x.Id == messageId && !x.IsDeleted && x.Status == OutboxMessageStatus.Pending,
            cancellationToken);

        if (entity == null)
        {
            return false;
        }

        entity.Status = OutboxMessageStatus.Processing;
        entity.Attempts += 1;
        entity.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<OutboxMessage?> FindMessageByIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return _dbContext.OutboxMessages.FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);
    }

    public Task<OutboxDelivery?> FindDeliveryAsync(Guid eventId, string handlerName, CancellationToken cancellationToken = default)
    {
        return _dbContext.OutboxDeliveries.FirstOrDefaultAsync(
            x => x.EventId == eventId && x.HandlerName == handlerName,
            cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxDelivery>> GetDispatchableDeliveriesAsync(int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return await _dbContext.OutboxDeliveries
            .Where(x => !x.IsDeleted
                        && x.Status == OutboxDeliveryStatus.Pending
                        && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryMarkDeliveryProcessingAsync(Guid deliveryId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.OutboxDeliveries.FirstOrDefaultAsync(
            x => x.Id == deliveryId
                 && !x.IsDeleted
                 && x.Status == OutboxDeliveryStatus.Pending
                 && (x.NextAttemptAt == null || x.NextAttemptAt <= now),
            cancellationToken);

        if (entity == null)
        {
            return false;
        }

        entity.Status = OutboxDeliveryStatus.Processing;
        entity.Attempts += 1;
        entity.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<OutboxDelivery?> FindDeliveryByIdWithEventAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        return _dbContext.OutboxDeliveries
            .Include(x => x.Event)
            .FirstOrDefaultAsync(x => x.Id == deliveryId, cancellationToken);
    }
}
