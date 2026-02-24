using LgymApi.DataSeeder.Seeders;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Tests;

[TestFixture]
public sealed class SeederSmokeExtensionTests
{
    [Test]
    public async Task RoleSeeder_Should_Add_Default_Roles()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var seeder = new RoleSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var names = await context.Roles.Select(role => role.Name).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain(AuthConstants.Roles.Admin));
            Assert.That(names, Does.Contain(AuthConstants.Roles.User));
            Assert.That(names, Does.Contain(AuthConstants.Roles.Trainer));
            Assert.That(names, Does.Contain(AuthConstants.Roles.Tester));
        });
    }

    [Test]
    public async Task RoleClaimSeeder_Should_Add_Admin_Permission_Claims()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var roleSeeder = new RoleSeeder();
        await roleSeeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var seeder = new RoleClaimSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var adminClaims = await context.RoleClaims
            .Where(claim => claim.RoleId == AppDbContext.AdminRoleSeedId)
            .Select(claim => claim.ClaimValue)
            .ToListAsync();

        Assert.That(adminClaims, Does.Contain(AuthConstants.Permissions.AdminAccess));
        Assert.That(adminClaims, Does.Contain(AuthConstants.Permissions.ManageUserRoles));
    }

    [Test]
    public async Task TrainerInvitationSeeder_Should_Add_Invitation_For_Demo_Users()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        seedContext.DemoUsers.Add(new User { Id = Guid.NewGuid(), Name = "Trainer" });
        seedContext.DemoUsers.Add(new User { Id = Guid.NewGuid(), Name = "Trainee" });

        var seeder = new TrainerInvitationSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.TrainerInvitations.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task TrainerTraineeLinkSeeder_Should_Add_Link_For_Demo_Users()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        seedContext.DemoUsers.Add(new User { Id = Guid.NewGuid(), Name = "Trainer" });
        seedContext.DemoUsers.Add(new User { Id = Guid.NewGuid(), Name = "Trainee" });

        var seeder = new TrainerTraineeLinkSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.TrainerTraineeLinks.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task ReportTemplateSeeder_Should_Add_Template_With_Fields()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        seedContext.DemoUsers.Add(new User { Id = Guid.NewGuid(), Name = "Trainer" });

        var seeder = new ReportTemplateSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.ReportTemplates.CountAsync(), Is.EqualTo(1));
        Assert.That(await context.ReportTemplateFields.CountAsync(), Is.GreaterThan(0));
    }

    [Test]
    public async Task ReportRequestSeeder_Should_Add_Request_And_Submission()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var trainer = new User { Id = Guid.NewGuid(), Name = "Trainer" };
        var trainee = new User { Id = Guid.NewGuid(), Name = "Trainee" };
        seedContext.DemoUsers.Add(trainer);
        seedContext.DemoUsers.Add(trainee);

        var templateSeeder = new ReportTemplateSeeder();
        await templateSeeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var seeder = new ReportRequestSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.ReportRequests.CountAsync(), Is.EqualTo(1));
        Assert.That(await context.ReportSubmissions.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task SupplementPlanSeeder_Should_Add_Plan_Items_And_Log()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        seedContext.DemoUsers.Add(new User { Id = Guid.NewGuid(), Name = "Trainer" });
        seedContext.DemoUsers.Add(new User { Id = Guid.NewGuid(), Name = "Trainee" });

        var planSeeder = new SupplementPlanSeeder();
        await planSeeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var itemSeeder = new SupplementPlanItemSeeder();
        await itemSeeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var logSeeder = new SupplementIntakeLogSeeder();
        await logSeeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.SupplementPlans.CountAsync(), Is.EqualTo(1));
        Assert.That(await context.SupplementPlanItems.CountAsync(), Is.GreaterThan(0));
        Assert.That(await context.SupplementIntakeLogs.CountAsync(), Is.GreaterThan(0));
    }

    [Test]
    public async Task EmailNotificationLogSeeder_Should_Add_Logs()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var seeder = new EmailNotificationLogSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.EmailNotificationLogs.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task ReportSubmissionSeeder_Should_Skip_When_No_Requests()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var seeder = new ReportSubmissionSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.ReportSubmissions.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task ReportTemplateFieldSeeder_Should_Add_Fields_For_Seeded_Template()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var trainer = new User { Id = Guid.NewGuid(), Name = "Trainer" };
        seedContext.DemoUsers.Add(trainer);

        var templateSeeder = new ReportTemplateSeeder();
        await templateSeeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var fieldSeeder = new ReportTemplateFieldSeeder();
        await fieldSeeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.ReportTemplateFields.CountAsync(), Is.GreaterThan(0));
    }

    private static async Task<AppDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
