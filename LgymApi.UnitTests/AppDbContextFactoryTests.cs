using FluentAssertions;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class AppDbContextFactoryTests
{
    private const string EnvironmentVariableName = "ConnectionStrings__Postgres";
    private string? _originalValue;

    [SetUp]
    public void SetUp()
    {
        _originalValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariableName, _originalValue);
    }

    [Test]
    public void CreateDbContext_WhenEnvironmentVariableMissing_UsesDefaultConnectionString()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariableName, null);
        var factory = new AppDbContextFactory();

        using var context = factory.CreateDbContext([]);

        context.Database.GetConnectionString().Should().Contain("Host=localhost;Port=5433;Database=LGYM-APP");
        context.Database.GetConnectionString().Should().Contain("Password=REPLACE_ME");
        context.Database.GetConnectionString().Should().NotContain("sasasa");
    }

    [Test]
    public void CreateDbContext_WhenEnvironmentVariableProvided_UsesOverride()
    {
        const string connectionString = "Host=prod;Port=5432;Database=LGYM;Username=test;Password=REPLACE_ME_IN_TEST";
        Environment.SetEnvironmentVariable(EnvironmentVariableName, connectionString);
        var factory = new AppDbContextFactory();

        using var context = factory.CreateDbContext([]);

        context.Database.GetConnectionString().Should().Be(connectionString);
    }
}
