-- Synthetic large-partial catalog for query profiling (see docs/performance.md).
-- The real MusicBrainz dump is multi-gigabyte; this fills the migrated schema
-- with enough volume (3M recordings) that plan choices match production shape.
--
--   createdb -h localhost -p 5442 -U musiql musiql_perf
--   MUSIQL_CONNECTION="Host=localhost;Port=5442;Database=musiql_perf;Username=musiql;Password=musiql" \
--     dotnet ef database update --project src/MusiQL.Data --context MusiQLDbContext
--   psql -h localhost -p 5442 -U musiql -d musiql_perf -f scripts/perf-seed.sql
--
-- Throwaway data only. Drop the database when finished.
SET synchronous_commit = off;

INSERT INTO catalog.genre (id, mbid, name)
SELECT g, gen_random_uuid(), 'genre_' || g
FROM generate_series(1, 2000) g;

INSERT INTO catalog.genre (id, mbid, name) VALUES
 (900001, gen_random_uuid(), 'grunge'),
 (900002, gen_random_uuid(), 'alternative rock'),
 (900003, gen_random_uuid(), 'rock'),
 (900004, gen_random_uuid(), 'punk');

INSERT INTO catalog.artist (id, mbid, name, sort_name, begin_year, end_year)
SELECT a, gen_random_uuid(), 'Artist ' || a, 'Artist ' || a,
       (1950 + (a % 70))::smallint, NULL
FROM generate_series(1, 300000) a;

INSERT INTO catalog.release_group (id, mbid, name, artist_id, primary_type, first_release_year)
SELECT rg, gen_random_uuid(), 'Album ' || rg,
       1 + (random() * 299999)::int,
       (ARRAY['Album','Single','EP'])[1 + (random() * 2)::int],
       (1950 + (random() * 74)::int)::smallint
FROM generate_series(1, 800000) rg;

INSERT INTO catalog.recording (id, mbid, name, artist_id, length_ms, release_group_id, first_release_year)
SELECT rec, gen_random_uuid(), 'Track ' || rec,
       1 + (random() * 299999)::int,
       (60000 + (random() * 540000)::int),
       CASE WHEN random() < 0.95 THEN 1 + (random() * 799999)::int ELSE NULL END,
       (1950 + (random() * 74)::int)::smallint
FROM generate_series(1, 3000000) rec;

INSERT INTO catalog.artist_genre (artist_id, genre_id, votes)
SELECT a, 1 + (random() * 1999)::int, (random() * 10)::int
FROM generate_series(1, 300000) a
ON CONFLICT DO NOTHING;

INSERT INTO catalog.artist_genre (artist_id, genre_id, votes)
SELECT a, 1 + (random() * 1999)::int, (random() * 10)::int
FROM generate_series(1, 300000) a WHERE random() < 0.4
ON CONFLICT DO NOTHING;

INSERT INTO catalog.artist_genre (artist_id, genre_id, votes)
SELECT a, 900001, 6 FROM generate_series(1, 300000) a WHERE random() < 0.001
ON CONFLICT DO NOTHING;

INSERT INTO catalog.release_group_genre (release_group_id, genre_id, votes)
SELECT rg, 1 + (random() * 1999)::int, (random() * 10)::int
FROM generate_series(1, 800000) rg
ON CONFLICT DO NOTHING;

INSERT INTO catalog.recording_genre (recording_id, genre_id, votes)
SELECT rec, 1 + (random() * 1999)::int, (random() * 10)::int
FROM generate_series(1, 3000000) rec WHERE random() < 0.7
ON CONFLICT DO NOTHING;

INSERT INTO catalog.recording_genre (recording_id, genre_id, votes)
SELECT rec, 900001, 7 FROM generate_series(1, 3000000) rec WHERE random() < 0.001
ON CONFLICT DO NOTHING;

INSERT INTO catalog.recording_genre (recording_id, genre_id, votes)
SELECT rec, 900002, 7 FROM generate_series(1, 3000000) rec WHERE random() < 0.002
ON CONFLICT DO NOTHING;

INSERT INTO app.user_library (user_id, recording_id, added_at)
SELECT '11111111-1111-1111-1111-111111111111', rec, now()
FROM generate_series(1, 3000000) rec WHERE random() < 0.0017
ON CONFLICT DO NOTHING;

ANALYZE;
