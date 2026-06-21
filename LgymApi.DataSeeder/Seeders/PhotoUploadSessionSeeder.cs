using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class PhotoUploadSessionSeeder : IEntitySeeder
{
    public int Order => 88;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("photo upload sessions");
        SeedOperationConsole.Skip("photo upload sessions");
        return Task.CompletedTask;
    }
}
