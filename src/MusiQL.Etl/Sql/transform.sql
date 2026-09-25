CREATE TABLE staging.credit_artist AS
SELECT DISTINCT ON (artist_credit) artist_credit, artist AS artist_id
FROM staging.artist_credit_name
ORDER BY artist_credit, position;
CREATE UNIQUE INDEX ON staging.credit_artist (artist_credit);

CREATE TABLE staging.official_release AS
SELECT r.id, r.gid, r.name, r.artist_credit, r.release_group
FROM staging.release r
JOIN staging.release_status s ON s.id = r.status
WHERE s.name = 'Official';
CREATE INDEX ON staging.official_release (id);
CREATE INDEX ON staging.official_release (release_group);

CREATE TABLE staging.live_rg AS
SELECT DISTINCT release_group AS id FROM staging.official_release;
CREATE UNIQUE INDEX ON staging.live_rg (id);

CREATE TABLE staging.release_group_out AS
SELECT rg.id,
       rg.gid AS mbid,
       rg.name,
       ca.artist_id,
       pt.name AS primary_type,
       -- MusicBrainz has a few hundred typo'd future dates (e.g. 2913); treat
       -- them as unknown so they do not top every "order by year desc".
       CASE WHEN m.first_release_year <= extract(year FROM now()) THEN m.first_release_year END
           AS first_release_year
FROM staging.release_group rg
JOIN staging.live_rg lr ON lr.id = rg.id
JOIN staging.credit_artist ca ON ca.artist_credit = rg.artist_credit
JOIN staging.artist a ON a.id = ca.artist_id
LEFT JOIN staging.release_group_primary_type pt ON pt.id = rg.type
LEFT JOIN staging.release_group_meta m ON m.id = rg.id;
CREATE UNIQUE INDEX ON staging.release_group_out (id);

CREATE TABLE staging.recording_rg AS
SELECT DISTINCT t.recording AS recording_id, orl.release_group AS rg_id
FROM staging.track t
JOIN staging.medium md ON md.id = t.medium
JOIN staging.official_release orl ON orl.id = md.release;

CREATE TABLE staging.recording_canonical AS
SELECT DISTINCT ON (rr.recording_id)
       rr.recording_id, rr.rg_id, rgo.first_release_year
FROM staging.recording_rg rr
JOIN staging.release_group_out rgo ON rgo.id = rr.rg_id
ORDER BY rr.recording_id, rgo.first_release_year NULLS LAST, rgo.id;
CREATE UNIQUE INDEX ON staging.recording_canonical (recording_id);

CREATE TABLE staging.recording_out AS
SELECT rec.id,
       rec.gid AS mbid,
       rec.name,
       ca.artist_id,
       rec.length AS length_ms,
       rc.rg_id AS release_group_id,
       rc.first_release_year
FROM staging.recording rec
JOIN staging.recording_canonical rc ON rc.recording_id = rec.id
JOIN staging.credit_artist ca ON ca.artist_credit = rec.artist_credit
JOIN staging.artist a ON a.id = ca.artist_id;

CREATE TABLE staging.artist_out AS
SELECT a.id, a.gid AS mbid, a.name, a.sort_name,
       CASE WHEN a.begin_year <= extract(year FROM now()) THEN a.begin_year END AS begin_year,
       CASE WHEN a.end_year <= extract(year FROM now()) THEN a.end_year END AS end_year
FROM staging.artist a
WHERE a.id IN (SELECT artist_id FROM staging.release_group_out
               UNION SELECT artist_id FROM staging.recording_out);
CREATE UNIQUE INDEX ON staging.artist_out (id);

CREATE TABLE staging.genre_out AS
SELECT id, gid AS mbid, name FROM staging.genre;

CREATE TABLE staging.tag_genre AS
SELECT t.id AS tag_id, g.id AS genre_id
FROM staging.tag t
JOIN staging.genre g ON lower(g.name) = lower(t.name);
CREATE UNIQUE INDEX ON staging.tag_genre (tag_id);

CREATE TABLE staging.artist_genre_out AS
SELECT at.artist AS artist_id, tg.genre_id, sum(at.count)::int AS votes
FROM staging.artist_tag at
JOIN staging.tag_genre tg ON tg.tag_id = at.tag
JOIN staging.artist_out ao ON ao.id = at.artist
GROUP BY at.artist, tg.genre_id
HAVING sum(at.count) > 0;

CREATE TABLE staging.release_group_genre_out AS
SELECT rt.release_group AS release_group_id, tg.genre_id, sum(rt.count)::int AS votes
FROM staging.release_group_tag rt
JOIN staging.tag_genre tg ON tg.tag_id = rt.tag
JOIN staging.release_group_out rgo ON rgo.id = rt.release_group
GROUP BY rt.release_group, tg.genre_id
HAVING sum(rt.count) > 0;

CREATE TABLE staging.recording_genre_out AS
SELECT rt.recording AS recording_id, tg.genre_id, sum(rt.count)::int AS votes
FROM staging.recording_tag rt
JOIN staging.tag_genre tg ON tg.tag_id = rt.tag
JOIN staging.recording_out ro ON ro.id = rt.recording
GROUP BY rt.recording, tg.genre_id
HAVING sum(rt.count) > 0;

-- A genre only counts for an entity when it carries at least a quarter of the
-- votes of that entity's top tag. One or two stray "grunge" votes on R.E.M.
-- would otherwise make every R.E.M. track grunge, and popularity ranking puts
-- exactly those famous artists first. Single-tag entities are unaffected.
DELETE FROM staging.artist_genre_out o
USING (SELECT artist_id, max(votes) AS top FROM staging.artist_genre_out GROUP BY artist_id) t
WHERE t.artist_id = o.artist_id AND o.votes < 0.25 * t.top;

DELETE FROM staging.release_group_genre_out o
USING (SELECT release_group_id, max(votes) AS top FROM staging.release_group_genre_out GROUP BY release_group_id) t
WHERE t.release_group_id = o.release_group_id AND o.votes < 0.25 * t.top;

DELETE FROM staging.recording_genre_out o
USING (SELECT recording_id, max(votes) AS top FROM staging.recording_genre_out GROUP BY recording_id) t
WHERE t.recording_id = o.recording_id AND o.votes < 0.25 * t.top;
