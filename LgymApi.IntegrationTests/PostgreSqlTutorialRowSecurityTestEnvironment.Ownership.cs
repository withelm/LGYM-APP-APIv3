using Npgsql;

namespace LgymApi.IntegrationTests;

internal sealed partial class PostgreSqlTutorialRowSecurityTestEnvironment
{
    private async Task TransferApplicationOwnershipAsync()
    {
        await using var connection = new NpgsqlConnection(MaintenanceConnectionString);
        await connection.OpenAsync();
        await ExecuteFormattedAsync(connection, "ALTER SCHEMA public OWNER TO %I", _runtimeRole);
        await using var command = new NpgsqlCommand("""
            SELECT set_config('lgym.runtime_role', @runtimeRole, false);
            SELECT set_config('lgym.maintenance_role', @maintenanceRole, false);
            DO $ownership$
            DECLARE
                object record;
            BEGIN
                FOR object IN
                    SELECT namespace.nspname, relation.relname, relation.relkind
                    FROM pg_class relation
                    JOIN pg_namespace namespace ON namespace.oid = relation.relnamespace
                    JOIN pg_roles owner ON owner.oid = relation.relowner
                    WHERE namespace.nspname = 'public'
                      AND owner.rolname = current_setting('lgym.maintenance_role')
                      AND relation.relkind IN ('r', 'p', 'S', 'v', 'm', 'f')
                LOOP
                    EXECUTE format(
                        CASE object.relkind
                            WHEN 'S' THEN 'ALTER SEQUENCE %I.%I OWNER TO %I'
                            WHEN 'v' THEN 'ALTER VIEW %I.%I OWNER TO %I'
                            WHEN 'm' THEN 'ALTER MATERIALIZED VIEW %I.%I OWNER TO %I'
                            WHEN 'f' THEN 'ALTER FOREIGN TABLE %I.%I OWNER TO %I'
                            ELSE 'ALTER TABLE %I.%I OWNER TO %I'
                        END,
                        object.nspname,
                        object.relname,
                        current_setting('lgym.runtime_role'));
                END LOOP;
            END
            $ownership$;
            """, connection);
        command.Parameters.AddWithValue("runtimeRole", _runtimeRole);
        command.Parameters.AddWithValue("maintenanceRole", _maintenanceRole);
        await command.ExecuteNonQueryAsync();
        await ExecuteFormattedAsync(connection, "GRANT USAGE ON SCHEMA public TO %I", _maintenanceRole);
        await ExecuteFormattedAsync(connection, "GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO %I", _maintenanceRole);
        await ExecuteFormattedAsync(connection, "GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO %I", _maintenanceRole);
    }
}
