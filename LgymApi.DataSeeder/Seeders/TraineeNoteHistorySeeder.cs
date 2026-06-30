using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class TraineeNoteHistorySeeder : IEntitySeeder
{
    public int Order => 92;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("trainee note history");
        SeedOperationConsole.Skip("trainee note history");
        return Task.CompletedTask;
    }
}
