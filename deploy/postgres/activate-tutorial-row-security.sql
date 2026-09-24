\set ON_ERROR_STOP on
\set tutorial_row_security_is_activation true
\ir tutorial-row-security-preflight.sql

WITH expressions(parent_expression, child_expression) AS (
    VALUES (
        $policy$("UserId" =
        CASE
            WHEN (current_setting('lgym.account_id'::text, true) ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) THEN (current_setting('lgym.account_id'::text, true))::uuid
            ELSE NULL::uuid
        END)$policy$,
        $policy$(EXISTS ( SELECT 1
           FROM "UserTutorialProgresses" progress
          WHERE ((progress."Id" = "UserTutorialStepProgresses"."UserTutorialProgressId") AND (progress."UserId" =
                CASE
                    WHEN (current_setting('lgym.account_id'::text, true) ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) THEN (current_setting('lgym.account_id'::text, true))::uuid
                    ELSE NULL::uuid
                END))))$policy$
    )
), normalized(parent_expression, child_expression) AS (
    SELECT regexp_replace(parent_expression, '[[:space:]]+', '', 'g'),
           regexp_replace(child_expression, '[[:space:]]+', '', 'g')
    FROM expressions
), expected(table_name, policy_name, policy_command, policy_roles, is_permissive, using_expression, check_expression) AS (
    SELECT 'UserTutorialProgresses', 'user_tutorial_progresses_actor_select', 'r', ARRAY['PUBLIC']::text[], true, parent_expression, NULL FROM normalized
    UNION ALL SELECT 'UserTutorialProgresses', 'user_tutorial_progresses_actor_insert', 'a', ARRAY['PUBLIC']::text[], true, NULL, parent_expression FROM normalized
    UNION ALL SELECT 'UserTutorialProgresses', 'user_tutorial_progresses_actor_update', 'w', ARRAY['PUBLIC']::text[], true, parent_expression, parent_expression FROM normalized
    UNION ALL SELECT 'UserTutorialProgresses', 'user_tutorial_progresses_actor_delete', 'd', ARRAY['PUBLIC']::text[], true, parent_expression, NULL FROM normalized
    UNION ALL SELECT 'UserTutorialStepProgresses', 'user_tutorial_step_progresses_actor_select', 'r', ARRAY['PUBLIC']::text[], true, child_expression, NULL FROM normalized
    UNION ALL SELECT 'UserTutorialStepProgresses', 'user_tutorial_step_progresses_actor_insert', 'a', ARRAY['PUBLIC']::text[], true, NULL, child_expression FROM normalized
    UNION ALL SELECT 'UserTutorialStepProgresses', 'user_tutorial_step_progresses_actor_update', 'w', ARRAY['PUBLIC']::text[], true, child_expression, child_expression FROM normalized
    UNION ALL SELECT 'UserTutorialStepProgresses', 'user_tutorial_step_progresses_actor_delete', 'd', ARRAY['PUBLIC']::text[], true, child_expression, NULL FROM normalized
), actual(table_name, policy_name, policy_command, policy_roles, is_permissive, using_expression, check_expression) AS (
    SELECT relation.relname,
           policy.polname,
           policy.polcmd::text,
           ARRAY(
               SELECT CASE WHEN policy_role.role_oid = 0 THEN 'PUBLIC' ELSE role.rolname END
               FROM unnest(policy.polroles) AS policy_role(role_oid)
               LEFT JOIN pg_roles role ON role.oid = policy_role.role_oid
               ORDER BY 1),
           policy.polpermissive,
           regexp_replace(pg_get_expr(policy.polqual, policy.polrelid), '[[:space:]]+', '', 'g'),
           regexp_replace(pg_get_expr(policy.polwithcheck, policy.polrelid), '[[:space:]]+', '', 'g')
    FROM pg_policy policy
    JOIN pg_class relation ON relation.oid = policy.polrelid
    JOIN pg_namespace namespace ON namespace.oid = relation.relnamespace
    WHERE namespace.nspname = 'public'
      AND relation.relname IN ('UserTutorialProgresses', 'UserTutorialStepProgresses')
)
SELECT NOT EXISTS (
    (SELECT * FROM expected EXCEPT SELECT * FROM actual)
    UNION ALL
    (SELECT * FROM actual EXCEPT SELECT * FROM expected)
) AS policy_contract_matches \gset
\if :policy_contract_matches
\else
  \echo 'Tutorial RLS policy contract does not match the dormant migration.'
  SELECT 1 / 0;
\endif

ALTER TABLE public."UserTutorialProgresses" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."UserTutorialProgresses" FORCE ROW LEVEL SECURITY;
ALTER TABLE public."UserTutorialStepProgresses" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."UserTutorialStepProgresses" FORCE ROW LEVEL SECURITY;

COMMIT;
