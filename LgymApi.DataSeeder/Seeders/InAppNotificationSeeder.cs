using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class InAppNotificationSeeder : IEntitySeeder
{
    public int Order => 69;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("in-app notifications");

        var recipient = seedContext.AdminUser ?? seedContext.TesterUser;
        if (recipient == null)
        {
            SeedOperationConsole.Skip("in-app notifications");
            return;
        }

        if (seedContext.InAppNotifications.Count > 0)
        {
            SeedOperationConsole.Skip("in-app notifications");
            return;
        }

        var message = $"SEED_NOTIFICATION_{recipient.Id}";
        var exists = await context.InAppNotifications
            .AsNoTracking()
            .AnyAsync(notification => notification.RecipientId == recipient.Id && notification.Message == message, cancellationToken);

        if (exists)
        {
            SeedOperationConsole.Skip("in-app notifications");
            return;
        }

        var notificationEntity = new InAppNotification
        {
            Id = Id<InAppNotification>.New(),
            RecipientId = recipient.Id,
            IsSystemNotification = true,
            Message = message,
            RedirectUrl = "/app/notifications",
            IsRead = false,
            Type = InAppNotificationTypes.InvitationSent
        };

        await context.InAppNotifications.AddAsync(notificationEntity, cancellationToken);
        seedContext.InAppNotifications.Add(notificationEntity);
        SeedOperationConsole.Done("in-app notifications");
    }
}
