using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class EmailNotificationLogRepository : IEmailNotificationLogRepository
{
    private readonly AppDbContext _dbContext;

    public EmailNotificationLogRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        await _dbContext.NotificationMessages.AddAsync(message, cancellationToken);
    }

    public Task<NotificationMessage?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.NotificationMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<NotificationMessage?> FindByCorrelationAsync(string type, Guid correlationId, string recipient, CancellationToken cancellationToken = default)
    {
        return _dbContext.NotificationMessages.FirstOrDefaultAsync(
            x => x.Channel == NotificationChannel.Email && x.Type == type && x.CorrelationId == correlationId && x.Recipient == recipient,
            cancellationToken);
    }
}
