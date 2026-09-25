-- Rebuild catalog.genre_top_recording: for every genre, its most listened-to
-- tracks, counting a track as a member when the track, its album or its artist
-- carries the genre (the same rule MQL genre filters use). Only tracks with
-- known popularity are ranked; the cap keeps the table a few hundred MB while
-- covering any query that returns at most a few thousand rows.
SET LOCAL work_mem = '256MB';

CREATE TEMP TABLE genre_member ON COMMIT DROP AS
SELECT rg.genre_id, rp.recording_id, rp.listeners
FROM catalog.recording_popularity rp
JOIN catalog.recording_genre rg ON rg.recording_id = rp.recording_id
UNION
SELECT rgg.genre_id, rp.recording_id, rp.listeners
FROM catalog.recording_popularity rp
JOIN catalog.recording r ON r.id = rp.recording_id
JOIN catalog.release_group_genre rgg ON rgg.release_group_id = r.release_group_id
UNION
SELECT ag.genre_id, rp.recording_id, rp.listeners
FROM catalog.recording_popularity rp
JOIN catalog.recording r ON r.id = rp.recording_id
JOIN catalog.artist_genre ag ON ag.artist_id = r.artist_id;

TRUNCATE catalog.genre_top_recording;

INSERT INTO catalog.genre_top_recording (genre_id, rank, recording_id, listeners)
SELECT genre_id, rank, recording_id, listeners
FROM (
    SELECT genre_id, recording_id, listeners,
           row_number() OVER (PARTITION BY genre_id ORDER BY listeners DESC, recording_id) AS rank
    FROM genre_member
) ranked
WHERE rank <= 10000;
