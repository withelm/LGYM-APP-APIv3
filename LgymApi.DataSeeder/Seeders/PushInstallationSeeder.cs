using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class PushInstallationSeeder : IEntitySeeder
{
    public int Order => 89;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("push installations");
        SeedOperationConsole.Skip("push installations");
        return Task.CompletedTask;
    }
}
