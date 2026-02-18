using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
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

    public async Task AddAsync(EmailNotificationLog log, CancellationToken cancellationToken = default)
    {
        await _dbContext.EmailNotificationLogs.AddAsync(log, cancellationToken);
    }

    public Task<EmailNotificationLog?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.EmailNotificationLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<EmailNotificationLog?> FindByCorrelationAsync(string type, Guid correlationId, string recipientEmail, CancellationToken cancellationToken = default)
    {
        return _dbContext.EmailNotificationLogs.FirstOrDefaultAsync(
            x => x.Type == type && x.CorrelationId == correlationId && x.RecipientEmail == recipientEmail,
            cancellationToken);
    }
}
