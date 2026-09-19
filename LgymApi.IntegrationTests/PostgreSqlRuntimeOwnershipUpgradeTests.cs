using FluentAssertions;
using Npgsql;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class PostgreSqlRuntimeOwnershipUpgradeTests
{
    [Test]
    public async Task OwnershipUpgrade_WhenRunTwice_TransfersMaintenanceOwnedRelationToRuntime()
    {
        await using var environment = await PostgreSqlTutorialRowSecurityTestEnvironment.CreateAsync(activate: false);
        await environment.ExecuteAdminFormattedAsync("CREATE TABLE public.%I (id integer)", "ownership_upgrade_probe");
        await environment.ExecuteAdminFormattedAsync("ALTER TABLE public.%I OWNER TO %I", "ownership_upgrade_probe", environment.MaintenanceRole);

        await PostgreSqlTutorialRowSecurityActivation.RunOwnershipUpgradeAsync(
            environment.AdminConnectionString, environment.DatabaseName, environment.MaintenanceRole, environment.RuntimeRole);

        await using var connection = new NpgsqlConnection(environment.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT owner.rolname FROM pg_class relation
            JOIN pg_namespace namespace ON namespace.oid = relation.relnamespace
            JOIN pg_roles owner ON owner.oid = relation.relowner
            WHERE namespace.nspname = 'public' AND relation.relname = 'ownership_upgrade_probe';
            """, connection);
        (await command.ExecuteScalarAsync()).Should().Be(environment.RuntimeRole);
    }
}
