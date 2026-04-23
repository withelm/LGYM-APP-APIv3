using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public sealed class UserExternalLoginSeeder : IEntitySeeder
{
    public int Order => 111;

    public Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Skip("user external logins");
        return Task.CompletedTask;
    }
}
