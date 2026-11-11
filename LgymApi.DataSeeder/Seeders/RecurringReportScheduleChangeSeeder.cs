using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class RecurringReportScheduleChangeSeeder : IEntitySeeder
{
    public int Order => 87;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("recurring report schedule changes");
        SeedOperationConsole.Skip("recurring report schedule changes");
        return Task.CompletedTask;
    }
}
