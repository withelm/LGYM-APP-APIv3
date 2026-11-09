using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class ExerciseMediaObjectFenceSeeder : IEntitySeeder
{
    public int Order => 89;
    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        // Object fences and permanent retirement authority belong to authenticated runtime operations only.
        SeedOperationConsole.Start("exercise media object fences");
        SeedOperationConsole.Skip("exercise media object fences");
        return Task.CompletedTask;
    }
}
