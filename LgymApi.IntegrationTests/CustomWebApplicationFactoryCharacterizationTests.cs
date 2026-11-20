using LgymApi.Domain.Security;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common.Notifications;
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

        Assert.That(database.Database.ProviderName, Is.EqualTo("Microsoft.EntityFrameworkCore.InMemory"));
        Assert.That(database.Database.EnsureCreated(), Is.False);

        var roleNames = await database.Roles.Select(role => role.Name).ToListAsync();

        Assert.That(roleNames, Does.Contain(AuthConstants.Roles.User), string.Format(System.Globalization.CultureInfo.InvariantCulture, AuthConstants.Roles.Admin, AuthConstants.Roles.Tester, AuthConstants.Roles.Trainer));
    }

    [Test]
    public async Task CustomWebApplicationFactory_SuppressesMigrationsAndUsesNoOpTransactions()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var migrationAttempt = () => database.Database.MigrateAsync();

        await Assert.ThatAsync(async () => { await migrationAttempt(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*relational-specific methods*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        await using var transaction = await unitOfWork.BeginTransactionAsync();
        await transaction.RollbackAsync();
    }

    [Test]
    public async Task CustomWebApplicationFactory_ReplacesLiveEmailAndFcmWithCapturedFakes()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        var assertedValue47 = (scope.ServiceProvider.GetServices<IEmailSender>()).ToArray();
        Assert.That(assertedValue47, Has.Exactly(1).Items);
        Assert.That(assertedValue47.Single(), Is.SameAs(factory.EmailSender));
        var assertedValue48 = (scope.ServiceProvider.GetServices<IPushProviderSender>()).ToArray();
        Assert.That(assertedValue48, Has.Exactly(1).Items);
        Assert.That(assertedValue48.Single(), Is.SameAs(factory.PushSender));
    }
}
