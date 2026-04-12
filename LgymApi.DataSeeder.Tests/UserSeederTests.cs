using LgymApi.Application.Services;
using LgymApi.DataSeeder.Seeders;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
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
            .UseInMemoryDatabase(Id<User>.New().ToString())
            .Options;

        await using var context = new AppDbContext(options);
        var seedContext = new SeedContext { SeedDemoData = false };

        var seeder = new UserSeeder(new FakeLegacyPasswordService());
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.Users.CountAsync()).Should().Be(2);
        seedContext.AdminUser.Should().NotBeNull();
        seedContext.TesterUser.Should().NotBeNull();
    }

    [Test]
    public async Task SeedAsync_Should_Create_Demo_Users_When_Enabled()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Id<User>.New().ToString())
            .Options;

        await using var context = new AppDbContext(options);
        var seedContext = new SeedContext { SeedDemoData = true };

        var seeder = new UserSeeder(new FakeLegacyPasswordService());
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        seedContext.DemoUsers.Count.Should().Be(2);
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
