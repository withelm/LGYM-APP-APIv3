using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class TrainerPhotoTypeSeeder : IEntitySeeder
{
    public int Order => 81;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("trainer photo types");
        SeedOperationConsole.Skip("trainer photo types");
        return Task.CompletedTask;
    }
}
