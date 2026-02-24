using LgymApi.Infrastructure.Data;

namespace LgymApi.DataSeeder.Seeders;

public interface IEntitySeeder
{
    int Order { get; }
    Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken);
}
