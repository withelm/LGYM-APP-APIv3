using FluentAssertions;
using LgymApi.Api.Configuration;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class StartupMigrationBootstrapTests
{
    [Test]
    public async Task ApplyAsync_WithNullApp_ThrowsArgumentNullException()
    {
        var act = () => StartupMigrationBootstrap.ApplyAsync(app: null!, "Testing");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestCase("")]
    [TestCase(" ")]
    public async Task ApplyAsync_WithBlankTestingEnvironmentName_ThrowsArgumentException(string testingEnvironmentName)
    {
        using var app = CreateApp("Development", services =>
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase($"startup-migration-{Id<StartupMigrationBootstrapTests>.New():N}"));
        });

        var act = () => StartupMigrationBootstrap.ApplyAsync(app, testingEnvironmentName);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task ApplyAsync_InTestingEnvironment_SkipsMigrationAttempt()
    {
        using var app = CreateApp("Testing", services =>
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase($"startup-migration-{Id<StartupMigrationBootstrapTests>.New():N}"));
        });

        var act = () => StartupMigrationBootstrap.ApplyAsync(app, "Testing");

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task ApplyAsync_InDevelopmentEnvironment_AttemptsMigration()
    {
        using var app = CreateApp("Development", services =>
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase($"startup-migration-{Id<StartupMigrationBootstrapTests>.New():N}"));
        });

        var act = () => StartupMigrationBootstrap.ApplyAsync(app, "Testing");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*relational-specific methods*");
    }

    [TestCase("Development")]
    [TestCase("Staging")]
    [TestCase("Production")]
    [TestCase("Preview")]
    public void ShouldApplyMigrations_InNonTestingEnvironment_ReturnsTrue(string environmentName)
    {
        StartupMigrationBootstrap.ShouldApplyMigrations(environmentName, "Testing").Should().BeTrue();
    }

    [TestCase("Development", false)]
    [TestCase("Testing", false)]
    [TestCase("Staging", true)]
    [TestCase("Production", true)]
    [TestCase("Preview", true)]
    public void RequiresRuntimeValidation_IsFailClosedOutsideDevelopmentAndTesting(string environmentName, bool expected)
    {
        StartupMigrationBootstrap.RequiresRuntimeValidation(environmentName).Should().Be(expected);
    }

    private static WebApplication CreateApp(string environmentName, Action<IServiceCollection> configureServices)
    {
        var options = new WebApplicationOptions
        {
            EnvironmentName = environmentName
        };

        var builder = WebApplication.CreateBuilder(options);
        configureServices(builder.Services);
        return builder.Build();
    }
}
