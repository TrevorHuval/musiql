-- Read-only role used only to execute compiled MQL queries.
-- Run once as a privileged user after migrations, then wire its credentials into
-- ConnectionStrings:Query. The API's migration/Identity connection keeps using
-- the owner role. Re-runnable: safe to apply again after a dump refresh.
--
-- The role can read the catalog and a user's library, and nothing else. It must
-- never see the Identity tables (password hashes) that live in app.*.
--
-- Replace the placeholder password before using this outside local development.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musiql_query') THEN
        CREATE ROLE musiql_query LOGIN PASSWORD 'change-me';
    END IF;
END $$;

GRANT USAGE ON SCHEMA catalog TO musiql_query;
GRANT USAGE ON SCHEMA app TO musiql_query;

GRANT SELECT ON ALL TABLES IN SCHEMA catalog TO musiql_query;
GRANT SELECT ON app.user_library TO musiql_query;

-- Keep catalog readable after a dump refresh recreates objects.
ALTER DEFAULT PRIVILEGES IN SCHEMA catalog GRANT SELECT ON TABLES TO musiql_query;
