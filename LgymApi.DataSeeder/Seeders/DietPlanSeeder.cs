using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class DietPlanSeeder : IEntitySeeder
{
    public int Order => 89;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("diet plans");
        SeedOperationConsole.Skip("diet plans");
        return Task.CompletedTask;
    }
}
