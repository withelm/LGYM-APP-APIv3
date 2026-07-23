using LgymApi.Domain.Security;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class CustomWebApplicationFactoryCharacterizationTests
{
    [Test]
    public async Task CustomWebApplicationFactory_ConfiguresInMemoryDatabaseAndSeedsRoles()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        database.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.InMemory");
        database.Database.EnsureCreated().Should().BeFalse();

        var roleNames = await database.Roles.Select(role => role.Name).ToListAsync();

        roleNames.Should().Contain(
            AuthConstants.Roles.User,
            AuthConstants.Roles.Admin,
            AuthConstants.Roles.Tester,
            AuthConstants.Roles.Trainer);
    }
}
