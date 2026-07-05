using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class DietMealSeeder : IEntitySeeder
{
    public int Order => 88;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("diet meals");
        SeedOperationConsole.Skip("diet meals");
        return Task.CompletedTask;
    }
}
