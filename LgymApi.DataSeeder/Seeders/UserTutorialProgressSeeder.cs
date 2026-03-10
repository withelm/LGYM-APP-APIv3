using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class UserTutorialProgressSeeder : IEntitySeeder
{
    public int Order => 1000;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Skip("user tutorial progress");
        return Task.CompletedTask;
    }
}
