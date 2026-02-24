using LgymApi.Application.Services;
using LgymApi.DataSeeder.Seeders;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Tests;

[TestFixture]
public sealed class SeedOrchestratorTests
{
    [Test]
    public async Task RunAsync_Should_Skip_Demo_Seeders_When_Disabled()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);
        var seedContext = new SeedContext();

        var seeders = new IEntitySeeder[]
        {
            new UserSeeder(new FakeLegacyPasswordService()),
            new EloRegistrySeeder(),
            new ExerciseSeeder()
        };

        var orchestrator = new SeedOrchestrator(seeders);

        var seedOptions = new SeedOptions
        {
            DropDatabase = false,
            UseMigrations = false,
            SeedDemoData = false
        };

        await orchestrator.RunAsync(context, seedContext, seedOptions, CancellationToken.None);

        Assert.That(await context.Exercises.CountAsync(), Is.EqualTo(0));
        Assert.That(seedContext.Exercises, Is.Empty);
    }

    [Test]
    public async Task RunAsync_Should_Set_Admin_And_Tester_When_Present_In_Database()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        var admin = new User { Id = Guid.NewGuid(), Name = "Admin" };
        var tester = new User { Id = Guid.NewGuid(), Name = "Tester" };
        context.Users.AddRange(admin, tester);
        await context.SaveChangesAsync();

        var seedContext = new SeedContext();
        var seeders = new IEntitySeeder[]
        {
            new UserSeeder(new FakeLegacyPasswordService()),
            new EloRegistrySeeder()
        };

        var orchestrator = new SeedOrchestrator(seeders);
        var seedOptions = new SeedOptions
        {
            DropDatabase = false,
            UseMigrations = false,
            SeedDemoData = false
        };

        await orchestrator.RunAsync(context, seedContext, seedOptions, CancellationToken.None);

        Assert.That(seedContext.AdminUser?.Name, Is.EqualTo("Admin"));
        Assert.That(seedContext.TesterUser?.Name, Is.EqualTo("Tester"));
    }

    private sealed class FakeLegacyPasswordService : ILegacyPasswordService
    {
        public bool Verify(string password, string hash, string salt, int? iterations, int? keyLength, string? digest)
        {
            return true;
        }

        public (string Hash, string Salt, int Iterations, int KeyLength, string Digest) Create(string password)
        {
            return ("hash", "salt", 1, 32, "sha");
        }
    }
}
