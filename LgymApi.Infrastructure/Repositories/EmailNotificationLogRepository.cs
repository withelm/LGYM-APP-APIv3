using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
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

    public Task<NotificationMessage?> FindByIdAsync(Id<NotificationMessage> id, CancellationToken cancellationToken = default)
    {
        return _dbContext.NotificationMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<NotificationMessage?> FindByCorrelationAsync(EmailNotificationType type, Id<CorrelationScope> correlationId, string recipient, CancellationToken cancellationToken = default)
    {
        return _dbContext.NotificationMessages.FirstOrDefaultAsync(
            x => x.Channel == NotificationChannel.Email && x.Type == type && x.CorrelationId == correlationId && x.Recipient == recipient,
            cancellationToken);
    }
}
