\set ON_ERROR_STOP on
\set tutorial_row_security_is_activation false
\ir tutorial-row-security-preflight.sql

ALTER TABLE public."UserTutorialStepProgresses" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE public."UserTutorialStepProgresses" DISABLE ROW LEVEL SECURITY;
ALTER TABLE public."UserTutorialProgresses" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE public."UserTutorialProgresses" DISABLE ROW LEVEL SECURITY;

COMMIT;
