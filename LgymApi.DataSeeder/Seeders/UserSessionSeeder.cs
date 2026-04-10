using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class UserSessionSeeder : IEntitySeeder
{
    public int Order => 110;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Skip("user sessions");
        return Task.CompletedTask;
    }
}
