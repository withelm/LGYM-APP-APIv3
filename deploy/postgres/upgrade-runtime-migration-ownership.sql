\set ON_ERROR_STOP on

\if :{?database_name}
\else
  \echo 'database_name is required'
  SELECT 1 / 0;
\endif
\if :{?maintenance_role}
\else
  \echo 'maintenance_role is required'
  SELECT 1 / 0;
\endif
\if :{?runtime_role}
\else
  \echo 'runtime_role is required'
  SELECT 1 / 0;
\endif

SELECT current_database() = :'database_name' AS target_database_matches \gset
\if :target_database_matches
\else
  \echo 'Connected database does not match database_name.'
  SELECT 1 / 0;
\endif

BEGIN;
SELECT pg_advisory_xact_lock(hashtextextended('lgym.runtime-migration-ownership', 0));
SELECT set_config('lgym.runtime_role', :'runtime_role', true);
SELECT set_config('lgym.maintenance_role', :'maintenance_role', true);
DO $preflight$
DECLARE
    maintenance_role_name text := current_setting('lgym.maintenance_role');
    runtime_role_name text := current_setting('lgym.runtime_role');
    maintenance_attributes pg_roles%ROWTYPE;
    runtime_attributes pg_roles%ROWTYPE;
BEGIN
    IF maintenance_role_name = runtime_role_name THEN
        RAISE EXCEPTION 'maintenance_role and runtime_role must differ';
    END IF;

    SELECT * INTO maintenance_attributes FROM pg_roles WHERE rolname = maintenance_role_name;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'maintenance role % does not exist', maintenance_role_name;
    END IF;

    IF maintenance_attributes.rolsuper OR NOT maintenance_attributes.rolbypassrls OR maintenance_attributes.rolcreaterole OR maintenance_attributes.rolcreatedb OR maintenance_attributes.rolreplication THEN
        RAISE EXCEPTION 'maintenance role % does not match the required security contract', maintenance_role_name;
    END IF;

    SELECT * INTO runtime_attributes FROM pg_roles WHERE rolname = runtime_role_name;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'runtime role % does not exist', runtime_role_name;
    END IF;

    IF runtime_attributes.rolsuper OR runtime_attributes.rolbypassrls OR runtime_attributes.rolcreaterole OR runtime_attributes.rolcreatedb OR runtime_attributes.rolreplication THEN
        RAISE EXCEPTION 'runtime role % does not match the required security contract', runtime_role_name;
    END IF;

    IF pg_has_role(runtime_role_name, maintenance_role_name, 'member') THEN
        RAISE EXCEPTION 'runtime role % may not assume maintenance role %', runtime_role_name, maintenance_role_name;
    END IF;
END
$preflight$;
GRANT :"runtime_role" TO :"maintenance_role";
ALTER SCHEMA public OWNER TO :"runtime_role";

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

GRANT USAGE ON SCHEMA public TO :"maintenance_role";
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO :"maintenance_role";
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO :"maintenance_role";
ALTER DEFAULT PRIVILEGES FOR ROLE :"runtime_role" IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"maintenance_role";
ALTER DEFAULT PRIVILEGES FOR ROLE :"runtime_role" IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO :"maintenance_role";
COMMIT;
