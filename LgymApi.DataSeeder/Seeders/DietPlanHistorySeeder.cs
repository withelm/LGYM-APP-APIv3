using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class DietPlanHistorySeeder : IEntitySeeder
{
    public int Order => 90;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("diet plan history");
        SeedOperationConsole.Skip("diet plan history");
        return Task.CompletedTask;
    }
}
