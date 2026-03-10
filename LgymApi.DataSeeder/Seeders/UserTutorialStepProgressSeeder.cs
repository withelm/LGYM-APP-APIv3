using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class UserTutorialStepProgressSeeder : IEntitySeeder
{
    public int Order => 1001;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Skip("user tutorial step progress");
        return Task.CompletedTask;
    }
}
