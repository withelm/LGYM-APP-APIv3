using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class TraineeNoteSeeder : IEntitySeeder
{
    public int Order => 91;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("trainee notes");
        SeedOperationConsole.Skip("trainee notes");
        return Task.CompletedTask;
    }
}
