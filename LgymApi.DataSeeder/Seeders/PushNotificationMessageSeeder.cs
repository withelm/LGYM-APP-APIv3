using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class PushNotificationMessageSeeder : IEntitySeeder
{
    public int Order => 90;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("push notification messages");
        SeedOperationConsole.Skip("push notification messages");
        return Task.CompletedTask;
    }
}
