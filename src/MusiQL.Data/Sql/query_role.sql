-- Read-only role used only to execute compiled MQL queries.
-- Run once as a privileged user after migrations. Phase 4 wires this role's
-- credentials into the query execution connection string; the API's own
-- migration/Identity connection keeps using the owner role.
--
-- The role can read the catalog and a user's library, and nothing else. It
-- must never see the Identity tables (password hashes) that live in app.*.

CREATE ROLE musiql_query LOGIN PASSWORD 'change-me';

GRANT USAGE ON SCHEMA catalog TO musiql_query;
GRANT USAGE ON SCHEMA app TO musiql_query;

GRANT SELECT ON ALL TABLES IN SCHEMA catalog TO musiql_query;
GRANT SELECT ON app.user_library TO musiql_query;

-- Keep catalog readable after a dump refresh recreates objects.
ALTER DEFAULT PRIVILEGES IN SCHEMA catalog GRANT SELECT ON TABLES TO musiql_query;
