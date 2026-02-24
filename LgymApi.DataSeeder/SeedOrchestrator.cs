using LgymApi.DataSeeder.Seeders;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder;

public sealed class SeedOrchestrator
{
    private readonly IReadOnlyList<IEntitySeeder> _seeders;

    public SeedOrchestrator(IEnumerable<IEntitySeeder> seeders)
    {
        _seeders = seeders.OrderBy(seeder => seeder.Order).ToList();
    }

    public async Task RunAsync(
        AppDbContext context,
        SeedContext seedContext,
        SeedOptions options,
        CancellationToken cancellationToken)
    {
        seedContext.SeedDemoData = options.SeedDemoData;
        if (options.DropDatabase)
        {
            Console.WriteLine("Dropping existing database...");
            await context.Database.EnsureDeletedAsync(cancellationToken);
        }

        if (options.UseMigrations)
        {
            Console.WriteLine("Applying migrations...");
            await context.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            Console.WriteLine("Ensuring database is created...");
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }

        var alwaysSeederTypes = new HashSet<Type>
        {
            typeof(UserSeeder),
            typeof(EloRegistrySeeder)
        };

        var alwaysSeeders = _seeders
            .Where(seeder => alwaysSeederTypes.Contains(seeder.GetType()))
            .ToList();

        foreach (var seeder in alwaysSeeders)
        {
            await seeder.SeedAsync(context, seedContext, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        if (!options.SeedDemoData)
        {
            Console.WriteLine("Demo data seeding skipped.");
        }
        else
        {
            var demoSeeders = _seeders
                .Where(seeder => !alwaysSeederTypes.Contains(seeder.GetType()))
                .ToList();

            foreach (var seeder in demoSeeders)
            {
                await seeder.SeedAsync(context, seedContext, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
            }

            Console.WriteLine("Demo data seeding completed.");
        }

        SeedStatisticsPrinter.PrintSummary(seedContext);
    }
}
