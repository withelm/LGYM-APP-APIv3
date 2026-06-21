using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class PhotoSeeder : IEntitySeeder
{
    public int Order => 87;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("photos");
        SeedOperationConsole.Skip("photos");
        return Task.CompletedTask;
    }
}
