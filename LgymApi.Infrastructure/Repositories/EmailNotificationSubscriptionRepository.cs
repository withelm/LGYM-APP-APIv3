using LgymApi.Application.Repositories;
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

    public Task<bool> IsSubscribedAsync(Guid userId, string notificationType, CancellationToken cancellationToken = default)
    {
        return _dbContext.EmailNotificationSubscriptions
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == userId && x.NotificationType == notificationType,
                cancellationToken);
    }
}
