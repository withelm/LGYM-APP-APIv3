using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Application.Notifications;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class InAppNotificationRepository : IInAppNotificationRepository
{
    private readonly AppDbContext _dbContext;

    public InAppNotificationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(InAppNotification notification, CancellationToken cancellationToken = default)
    {
        return _dbContext.InAppNotifications.AddAsync(notification, cancellationToken).AsTask();
    }

    public Task<InAppNotification?> GetByIdAsync(Id<InAppNotification> id, CancellationToken cancellationToken = default)
    {
        return _dbContext.InAppNotifications
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<InAppNotification>> GetPageAsync(
        Id<User> userId,
        int limit,
        DateTimeOffset? cursorCreatedAt,
        Id<User>? cursorId,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.InAppNotifications
            .AsNoTracking()
            .Where(x => x.RecipientId == userId && !x.IsDeleted);

        if (cursorCreatedAt.HasValue && cursorId.HasValue)
        {
            var cursorTs = cursorCreatedAt.Value;
            var cursorIdText = cursorId.Value.ToString();
            query = query.Where(x => x.CreatedAt < cursorTs || (x.CreatedAt == cursorTs && string.CompareOrdinal(x.Id.ToString(), cursorIdText) < 0));
        }

        var results = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id.ToString())
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return results;
    }

    public Task MarkAsReadAsync(Id<InAppNotification> id, CancellationToken cancellationToken = default)
    {
        return _dbContext.InAppNotifications
            .Where(x => x.Id == id)
            .ForEachAsync(x => x.IsRead = true, cancellationToken);
    }

    public Task MarkAllAsReadAsync(Id<User> userId, DateTimeOffset? before, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.InAppNotifications
            .Where(x => x.RecipientId == userId && !x.IsRead);

        if (before.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= before.Value);
        }

        return query.ForEachAsync(x => x.IsRead = true, cancellationToken);
    }

    public Task<int> GetUnreadCountAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.InAppNotifications
            .AsNoTracking()
            .CountAsync(x => x.RecipientId == userId && !x.IsRead && !x.IsDeleted, cancellationToken);
    }
}
