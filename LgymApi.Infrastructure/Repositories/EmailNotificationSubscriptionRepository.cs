using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class EmailNotificationSubscriptionRepository : IEmailNotificationSubscriptionRepository
{
    private readonly AppDbContext _dbContext;

    public EmailNotificationSubscriptionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> IsSubscribedAsync(Id<User> userId, string notificationType, CancellationToken cancellationToken = default)
    {
        return _dbContext.EmailNotificationSubscriptions
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == userId && x.NotificationType == notificationType,
                cancellationToken);
    }
}
