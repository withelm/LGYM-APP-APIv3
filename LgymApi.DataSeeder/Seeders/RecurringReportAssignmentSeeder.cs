using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class RecurringReportAssignmentSeeder : IEntitySeeder
{
    public int Order => 86;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("recurring report assignments");
        SeedOperationConsole.Skip("recurring report assignments");
        return Task.CompletedTask;
    }
}
