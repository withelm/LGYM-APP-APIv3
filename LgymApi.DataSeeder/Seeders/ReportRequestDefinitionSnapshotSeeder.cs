using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class ReportRequestDefinitionSnapshotSeeder : IEntitySeeder
{
    public int Order => 89;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("report request definition snapshots");
        SeedOperationConsole.Skip("report request definition snapshots");
        return Task.CompletedTask;
    }
}
