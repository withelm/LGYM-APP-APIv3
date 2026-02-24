using LgymApi.Application.Services;
using LgymApi.DataSeeder.Seeders;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Tests;

[TestFixture]
public sealed class UserSeederTests
{
    [Test]
    public async Task SeedAsync_Should_Create_Admin_And_Tester()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);
        var seedContext = new SeedContext { SeedDemoData = false };

        var seeder = new UserSeeder(new FakeLegacyPasswordService());
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.Users.CountAsync(), Is.EqualTo(2));
        Assert.That(seedContext.AdminUser, Is.Not.Null);
        Assert.That(seedContext.TesterUser, Is.Not.Null);
    }

    [Test]
    public async Task SeedAsync_Should_Create_Demo_Users_When_Enabled()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);
        var seedContext = new SeedContext { SeedDemoData = true };

        var seeder = new UserSeeder(new FakeLegacyPasswordService());
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(seedContext.DemoUsers.Count, Is.EqualTo(2));
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
