using FluentAssertions;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class PostgreSqlRuntimeRoleSafetyTests
{
    [TestCase(RuntimeRoleProperty.Superuser)]
    [TestCase(RuntimeRoleProperty.BypassRls)]
    public async Task RuntimeValidation_WhenRuntimeRoleIsElevated_FailsClosed(RuntimeRoleProperty property)
    {
        await using var environment = await PostgreSqlTutorialRowSecurityTestEnvironment.CreateAsync(activate: false);
        var attribute = property == RuntimeRoleProperty.Superuser ? "SUPERUSER" : "BYPASSRLS";
        var resetAttribute = property == RuntimeRoleProperty.Superuser ? "NOSUPERUSER" : "NOBYPASSRLS";
        await environment.ExecuteAdminFormattedAsync("ALTER ROLE %I " + attribute, environment.RuntimeRole);

        try
        {
            var action = () => PostgreSqlRuntimeCatalogInspectionTests.ValidateAsync(
                environment,
                PostgreSqlRuntimeCatalogInspectionTests.CreateConfiguration(environment));

            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*prohibited elevated attributes or role membership*");
        }
        finally
        {
            await environment.ExecuteAdminFormattedAsync("ALTER ROLE %I " + resetAttribute, environment.RuntimeRole);
        }
    }

    [TestCase("CREATEDB")]
    [TestCase("CREATEROLE")]
    [TestCase("REPLICATION")]
    public async Task RuntimeValidation_WhenRuntimeRoleCanSetAnInheritedProhibitedAttributeRole_FailsClosed(string attribute)
    {
        await using var environment = await PostgreSqlTutorialRowSecurityTestEnvironment.CreateAsync(activate: false);
        var elevatedRole = $"lgym_elevated_it_{Id<PostgreSqlRuntimeRoleSafetyTests>.New():N}";
        await environment.ExecuteAdminFormattedAsync("CREATE ROLE %I NOLOGIN NOSUPERUSER " + attribute, elevatedRole);
        await environment.ExecuteAdminFormattedAsync("GRANT %I TO %I", elevatedRole, environment.RuntimeRole);

        try
        {
            var action = () => PostgreSqlRuntimeCatalogInspectionTests.ValidateAsync(
                environment,
                PostgreSqlRuntimeCatalogInspectionTests.CreateConfiguration(environment));

            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*prohibited elevated attributes or role membership*");
        }
        finally
        {
            await environment.ExecuteAdminFormattedAsync("REVOKE %I FROM %I", elevatedRole, environment.RuntimeRole);
            await environment.ExecuteAdminFormattedAsync("DROP ROLE %I", elevatedRole);
        }
    }

    [Test]
    public async Task RuntimeRole_CannotEscalateButCanApplyApplicationSchemaDdl()
    {
        await using var environment = await PostgreSqlTutorialRowSecurityTestEnvironment.CreateAsync(activate: false);

        await AssertPermissionDeniedAsync(environment.RuntimeConnectionString, $"SET ROLE {environment.MaintenanceRole};");
        await ExecuteAsync(environment.RuntimeConnectionString, "CREATE TABLE public.runtime_schema_attempt (\"Id\" integer);");
        await ExecuteAsync(environment.RuntimeConnectionString, "DROP TABLE public.runtime_schema_attempt;");
        await PostgreSqlRuntimeCatalogInspectionTests.ValidateAsync(
            environment,
            PostgreSqlRuntimeCatalogInspectionTests.CreateConfiguration(environment));
    }

    [Test]
    public async Task RuntimeValidation_WhenRuntimeConnectionEnablesMultiplexing_FailsClosed()
    {
        await using var environment = await PostgreSqlTutorialRowSecurityTestEnvironment.CreateAsync(activate: false);
        var multiplexedConnectionString = new NpgsqlConnectionStringBuilder(environment.RuntimeConnectionString)
        {
            Multiplexing = true
        }.ConnectionString;
        await using var dbContext = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(multiplexedConnectionString).Options);
        var configuration = PostgreSqlRuntimeCatalogInspectionTests.CreateConfiguration(environment);

        var action = () => PostgreSqlRuntimeConnectionValidator.ValidateAsync(dbContext, configuration);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Runtime PostgreSQL connection must disable multiplexing for the RLS pilot.");
    }

    public enum RuntimeRoleProperty
    {
        Superuser,
        BypassRls
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertPermissionDeniedAsync(string connectionString, string sql)
    {
        var action = () => ExecuteAsync(connectionString, sql);
        var exception = await action.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be("42501");
    }
}
